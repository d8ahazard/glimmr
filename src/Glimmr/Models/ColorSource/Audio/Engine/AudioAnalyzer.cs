#region

using System;

#endregion

namespace Glimmr.Models.ColorSource.Audio.Engine;

/// <summary>
///     Converts a frame of FFT magnitudes from <see cref="ManagedBass" />
///     into an <see cref="AudioReactiveState" />. This is the single
///     hot-path DSP class in the audio pipeline; all array buffers are
///     pre-allocated (output arrays are double-buffered), so the only
///     per-frame allocation is the small state record itself.
/// </summary>
public sealed class AudioAnalyzer {
	private const int ChromaBins = 12;

	private readonly float[] _bands;
	private readonly float[] _bandsRaw;
	private readonly float[] _bandsSmoothed;
	private readonly float[] _chroma;
	private readonly float[] _chromaSmoothed;

	// Double-buffered output arrays handed to AudioReactiveState. Alternating between
	// two pre-allocated copies keeps the hot path allocation-free while consumers
	// still always see a fully written snapshot.
	private readonly float[][] _bandsOut;
	private readonly float[][] _bandsRawOut;
	private readonly float[][] _chromaOut;
	private int _outIndex;

	private readonly AutoGainController _agc;
	private readonly BeatDetector _beat;
	private float _attack = 0.55f;
	private AudioBandConfig _config;
	private float _decay = 0.30f;
	private long _frameIndex;
	private float _smoothedBeatPulse;

	public AudioAnalyzer(AudioBandConfig config) {
		_config = config;
		_bands = new float[config.BandCount];
		_bandsRaw = new float[config.BandCount];
		_bandsSmoothed = new float[config.BandCount];
		_chroma = new float[ChromaBins];
		_chromaSmoothed = new float[ChromaBins];
		_bandsOut = new[] { new float[config.BandCount], new float[config.BandCount] };
		_bandsRawOut = new[] { new float[config.BandCount], new float[config.BandCount] };
		_chromaOut = new[] { new float[ChromaBins], new float[ChromaBins] };
		_agc = new AutoGainController();
		_beat = new BeatDetector(config.BandCount);
	}

	public AudioBandConfig Config => _config;

	/// <summary>
	///     Replace the band configuration. Cheap; only invalidates the
	///     internal smoothing buffers when the band count changes.
	/// </summary>
	public void Reconfigure(AudioBandConfig config) {
		if (config.BandCount != _config.BandCount) {
			throw new ArgumentException(
				"BandCount changed; allocate a new AudioAnalyzer instead of reconfiguring.");
		}

		_config = config;
	}

	/// <summary>
	///     Set per-frame smoothing factors. <paramref name="attack" /> controls how
	///     fast bands rise toward new peaks; <paramref name="decay" /> controls
	///     how fast they fall.
	/// </summary>
	public void SetSmoothing(float attack, float decay) {
		_attack = Math.Clamp(attack, 0.01f, 1f);
		_decay = Math.Clamp(decay, 0.01f, 1f);
	}

	public void Reset() {
		Array.Clear(_bands, 0, _bands.Length);
		Array.Clear(_bandsRaw, 0, _bandsRaw.Length);
		Array.Clear(_bandsSmoothed, 0, _bandsSmoothed.Length);
		Array.Clear(_chroma, 0, _chroma.Length);
		Array.Clear(_chromaSmoothed, 0, _chromaSmoothed.Length);
		_agc.Reset();
		_beat.Reset();
		_smoothedBeatPulse = 0f;
		_frameIndex = 0;
	}

	/// <summary>
	///     Run the analyzer over a half-spectrum FFT magnitude buffer
	///     (BASS' <c>FFT1024</c> returns 512 magnitudes for a 1024-sample
	///     frame). <paramref name="userGain" />, <paramref name="beatSensitivity" />,
	///     <paramref name="noiseGate" /> are 0..1 user-facing knobs from the
	///     active scene / system data. <paramref name="stereoBalance" /> is
	///     -1..1 (left/right).
	/// </summary>
	public AudioReactiveState Analyze(float[] fft, int sampleRate, float deltaSeconds,
		float userGain, float beatSensitivity, float noiseGate, float stereoBalance) {
		if (fft == null || fft.Length == 0) {
			return AudioReactiveState.Empty;
		}

		if (deltaSeconds <= 0f) {
			deltaSeconds = 0.033f;
		}

		// Bin width in Hz for a half-spectrum buffer.
		var binWidth = sampleRate * 0.5f / fft.Length;

		// Aggregate bins into perceptual bands. We use mean magnitude per
		// band so wider bands don't wash out narrower ones.
		Array.Clear(_bandsRaw, 0, _bandsRaw.Length);
		var rms = 0f;
		var peak = 0f;
		var centroidNum = 0d;
		var centroidDen = 0d;

		for (var b = 0; b < _config.BandCount; b++) {
			var lo = _config.LowEdge[b];
			var hi = _config.HighEdge[b];
			var first = (int)MathF.Floor(lo / binWidth);
			var last = (int)MathF.Ceiling(hi / binWidth);
			if (first < 1) {
				first = 1;
			}

			if (last >= fft.Length) {
				last = fft.Length - 1;
			}

			var sum = 0f;
			var count = 0;
			for (var i = first; i <= last; i++) {
				var mag = fft[i];
				if (float.IsNaN(mag) || float.IsInfinity(mag) || mag < 0f) {
					mag = 0f;
				}

				// BASS returns linear magnitudes that need a perceptual taper
				// before they make a useful "loudness" estimate.
				var sc = MathF.Sqrt(mag);
				sum += sc;
				count++;
			}

			var mean = count > 0 ? sum / count : 0f;
			_bandsRaw[b] = mean;
			if (mean > peak) {
				peak = mean;
			}

			rms += mean * mean;
			centroidNum += mean * (lo + hi) * 0.5;
			centroidDen += mean;
		}

		rms = MathF.Sqrt(rms / _config.BandCount);
		var centroidHz = centroidDen > 0 ? (float)(centroidNum / centroidDen) : 0f;
		var centroidNorm = Math.Clamp(
			MathF.Log10(MathF.Max(1f, centroidHz) / 30f) / MathF.Log10(16000f / 30f), 0f, 1f);

		// Apply AGC + noise gate. Output is 0..1 with the gate baked in.
		Array.Copy(_bandsRaw, 0, _bands, 0, _bands.Length);
		_agc.Process(peak, _bands, deltaSeconds, userGain);

		// Extra hard noise gate that the user can crank up. Bands below
		// the floor are clamped to zero so the LEDs go dark on silence.
		if (noiseGate > 0f) {
			var floor = noiseGate * 0.3f;
			for (var i = 0; i < _bands.Length; i++) {
				_bands[i] = _bands[i] <= floor ? 0f : (_bands[i] - floor) / (1f - floor);
			}
		}

		// Asymmetric (attack/release) smoothing for visual continuity.
		var attackLerp = Math.Clamp(_attack, 0.01f, 1f);
		var decayLerp = Math.Clamp(_decay, 0.01f, 1f);
		for (var i = 0; i < _bands.Length; i++) {
			var current = _bandsSmoothed[i];
			var target = _bands[i];
			var lerp = target > current ? attackLerp : decayLerp;
			current += (target - current) * lerp;
			_bandsSmoothed[i] = current;
			_bands[i] = current;
		}

		// Beat detection on smoothed bands so single-frame spikes don't fire.
		var (beat, fluxNorm) = _beat.Process(_bandsSmoothed, deltaSeconds, beatSensitivity);

		// Smooth out the beat pulse so it has a visual tail even at 30fps.
		var pulse = _beat.Pulse;
		_smoothedBeatPulse = pulse > _smoothedBeatPulse
			? pulse
			: MathF.Max(0f, _smoothedBeatPulse - deltaSeconds * 3f);

		// Aggregate energy buckets across the configured bands.
		var (bass, lowMid, mid, highMid, treble) = AggregateRanges();

		// Chroma fold-down for musical color rotation effects.
		ComputeChroma(fft, binWidth, deltaSeconds);

		// Copy into the inactive output buffers instead of cloning - no allocations.
		_outIndex ^= 1;
		var bandsOut = _bandsOut[_outIndex];
		var bandsRawOut = _bandsRawOut[_outIndex];
		var chromaOut = _chromaOut[_outIndex];
		Array.Copy(_bands, bandsOut, _bands.Length);
		Array.Copy(_bandsRaw, bandsRawOut, _bandsRaw.Length);
		Array.Copy(_chromaSmoothed, chromaOut, _chromaSmoothed.Length);

		var state = new AudioReactiveState(
			bandsOut,
			bandsRawOut,
			bass, lowMid, mid, highMid, treble,
			Math.Clamp(rms, 0f, 1f),
			Math.Clamp(peak, 0f, 1f),
			centroidNorm,
			fluxNorm,
			_smoothedBeatPulse,
			beat,
			_beat.Bpm,
			Math.Clamp(stereoBalance, -1f, 1f),
			chromaOut,
			_agc.Gate,
			_agc.Gain,
			_frameIndex++,
			deltaSeconds);

		return state;
	}

	private (float bass, float lowMid, float mid, float highMid, float treble) AggregateRanges() {
		float bass = 0f, lowMid = 0f, mid = 0f, highMid = 0f, treble = 0f;
		int bC = 0, lmC = 0, mC = 0, hmC = 0, tC = 0;
		for (var i = 0; i < _config.BandCount; i++) {
			var f = _config.Center[i];
			var v = _bands[i];
			if (f < 150f) {
				bass += v;
				bC++;
			} else if (f < 500f) {
				lowMid += v;
				lmC++;
			} else if (f < 2000f) {
				mid += v;
				mC++;
			} else if (f < 6000f) {
				highMid += v;
				hmC++;
			} else {
				treble += v;
				tC++;
			}
		}

		float Avg(float sum, int n) {
			return n > 0 ? sum / n : 0f;
		}

		return (Avg(bass, bC), Avg(lowMid, lmC), Avg(mid, mC), Avg(highMid, hmC), Avg(treble, tC));
	}

	private void ComputeChroma(float[] fft, float binWidth, float deltaSeconds) {
		Array.Clear(_chroma, 0, _chroma.Length);
		// A4 = 440 Hz. Map each FFT bin into a pitch class via 12-TET.
		for (var i = 1; i < fft.Length; i++) {
			var hz = i * binWidth;
			if (hz < 65f || hz > 4200f) {
				continue;
			}

			var mag = fft[i];
			if (float.IsNaN(mag) || float.IsInfinity(mag) || mag <= 0f) {
				continue;
			}

			var midi = 69f + 12f * MathF.Log2(hz / 440f);
			var pc = ((int)MathF.Round(midi) % 12 + 12) % 12;
			_chroma[pc] += MathF.Sqrt(mag);
		}

		// Normalize then exponentially smooth.
		var max = 0f;
		for (var i = 0; i < ChromaBins; i++) {
			if (_chroma[i] > max) {
				max = _chroma[i];
			}
		}

		if (max > 0f) {
			for (var i = 0; i < ChromaBins; i++) {
				_chroma[i] /= max;
			}
		}

		var lerp = Math.Clamp(deltaSeconds * 6f, 0.05f, 1f);
		for (var i = 0; i < ChromaBins; i++) {
			_chromaSmoothed[i] += (_chroma[i] - _chromaSmoothed[i]) * lerp;
		}
	}
}
