#region

using System;

#endregion

namespace Glimmr.Models.ColorSource.Audio.Engine;

/// <summary>
///     Spectral-flux based onset / beat detector.
///     Compares the current spectrum to the previous one (positive energy
///     differences only), tracks an adaptive threshold from a moving
///     average + variance, and emits a beat when the deviation crosses
///     the threshold. A separate cool-down stops double-triggers.
/// </summary>
public sealed class BeatDetector {
	private readonly float[] _history;
	private readonly float _minIntervalSeconds;
	private readonly float[] _previousSpectrum;
	private readonly float _sensitivity;
	private float _bpm;
	private float _cooldown;
	private int _historyCount;
	private int _historyHead;
	private float _pulse;
	private float _timeSinceLastBeat;

	public BeatDetector(int spectrumLength, float sensitivity = 1.7f, int historyFrames = 60,
		float minIntervalSeconds = 0.18f) {
		_previousSpectrum = new float[spectrumLength];
		_history = new float[Math.Max(8, historyFrames)];
		_sensitivity = sensitivity;
		_minIntervalSeconds = minIntervalSeconds;
	}

	public float Pulse => _pulse;
	public float Bpm => _bpm;

	public void Reset() {
		Array.Clear(_previousSpectrum, 0, _previousSpectrum.Length);
		Array.Clear(_history, 0, _history.Length);
		_historyHead = 0;
		_historyCount = 0;
		_cooldown = 0f;
		_pulse = 0f;
		_timeSinceLastBeat = 0f;
		_bpm = 0f;
	}

	/// <summary>
	///     Returns (beatThisFrame, normalizedFlux). Flux is scaled to 0..1 by
	///     comparing against the recent moving-window peak.
	/// </summary>
	public (bool beat, float fluxNormalized) Process(float[] spectrum, float deltaSeconds, float userSensitivity) {
		if (deltaSeconds <= 0f) {
			deltaSeconds = 0.033f;
		}

		// Half-wave-rectified spectral flux: only positive energy increases
		// count toward an onset. We bias the bottom end which is where most
		// musical kicks live.
		var flux = 0f;
		var n = Math.Min(spectrum.Length, _previousSpectrum.Length);
		for (var i = 0; i < n; i++) {
			var prev = _previousSpectrum[i];
			var cur = spectrum[i];
			var delta = cur - prev;
			if (delta > 0f) {
				var weight = i < n / 4 ? 1.6f : 1f;
				flux += delta * weight;
			}

			_previousSpectrum[i] = cur;
		}

		flux /= MathF.Max(1f, n);

		// Update history ring buffer.
		_history[_historyHead] = flux;
		_historyHead = (_historyHead + 1) % _history.Length;
		_historyCount = Math.Min(_historyCount + 1, _history.Length);

		// Mean + variance over recent history form an adaptive threshold.
		float mean = 0f;
		float maxVal = 0f;
		for (var i = 0; i < _historyCount; i++) {
			mean += _history[i];
			if (_history[i] > maxVal) {
				maxVal = _history[i];
			}
		}

		mean /= MathF.Max(1f, _historyCount);

		var variance = 0f;
		for (var i = 0; i < _historyCount; i++) {
			var d = _history[i] - mean;
			variance += d * d;
		}

		variance = MathF.Sqrt(variance / MathF.Max(1f, _historyCount));

		var sens = Math.Clamp(userSensitivity, 0.1f, 4f) * _sensitivity;
		var threshold = mean + variance * sens;
		var fluxNormalized = maxVal > 0.0001f ? Math.Clamp(flux / maxVal, 0f, 1f) : 0f;

		_cooldown -= deltaSeconds;
		_timeSinceLastBeat += deltaSeconds;

		var beat = false;
		if (_cooldown <= 0f && _historyCount >= 8 && flux > threshold && flux > 0.0008f) {
			beat = true;
			_cooldown = _minIntervalSeconds;
			_pulse = 1f;

			// BPM estimate: convert the most recent inter-onset interval into
			// BPM. Heavy smoothing because our cadence is only ~30 fps.
			if (_timeSinceLastBeat is > 0.2f and < 2.0f) {
				var instBpm = 60f / _timeSinceLastBeat;
				_bpm = _bpm <= 0f ? instBpm : _bpm * 0.7f + instBpm * 0.3f;
			}

			_timeSinceLastBeat = 0f;
		} else {
			// Decay the pulse envelope.
			_pulse = MathF.Max(0f, _pulse - deltaSeconds * 4f);
		}

		return (beat, fluxNormalized);
	}
}
