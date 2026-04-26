#region

using System;

#endregion

namespace Glimmr.Models.ColorSource.Audio.Engine;

/// <summary>
///     Immutable snapshot of one analyzer frame.
///     Every value has been smoothed, AGC-compensated, and clipped to a
///     stable range so effect renderers don't have to do their own DSP.
/// </summary>
public sealed class AudioReactiveState {
	public static readonly AudioReactiveState Empty = new(
		new float[1], new float[1], 0f, 0f, 0f, 0f, 0f,
		0f, 0f, 0f, 0f, 0f, false, 0f, 0f,
		new float[12], 0f, 1f, 0, 0f);

	/// <summary>Smoothed, AGC-applied band magnitudes (0..1).</summary>
	public float[] Bands { get; }

	/// <summary>Pre-smoothing band magnitudes (0..1).</summary>
	public float[] BandsRaw { get; }

	/// <summary>Aggregated bass (~30-150 Hz) energy (0..1).</summary>
	public float Bass { get; }

	/// <summary>Aggregated low-mid energy (0..1).</summary>
	public float LowMid { get; }

	/// <summary>Aggregated mid-range energy (0..1).</summary>
	public float Mid { get; }

	/// <summary>Aggregated high-mid energy (0..1).</summary>
	public float HighMid { get; }

	/// <summary>Aggregated treble (~6 kHz+) energy (0..1).</summary>
	public float Treble { get; }

	/// <summary>RMS energy across the audible band (0..1).</summary>
	public float Rms { get; }

	/// <summary>Peak magnitude this frame (0..1).</summary>
	public float Peak { get; }

	/// <summary>Spectral centroid mapped to 0..1 across the band range.</summary>
	public float Centroid { get; }

	/// <summary>Normalized spectral flux (0..1).</summary>
	public float Flux { get; }

	/// <summary>Decaying envelope of the most recent beat (0..1).</summary>
	public float BeatPulse { get; }

	/// <summary>True for one frame when an onset is detected.</summary>
	public bool Beat { get; }

	/// <summary>Estimated tempo in beats per minute (0 if unknown).</summary>
	public float Bpm { get; }

	/// <summary>Stereo balance: -1 = left-only, 0 = centered, +1 = right-only.</summary>
	public float StereoBalance { get; }

	/// <summary>Per-pitch-class energy (0..1), C ... B.</summary>
	public float[] Chroma { get; }

	/// <summary>True when the noise gate is open.</summary>
	public bool Active => Rms > 0.001f;

	/// <summary>Noise-gate state, 0 closed → 1 fully open.</summary>
	public float Gate { get; }

	/// <summary>Current AGC multiplier, useful for diagnostics / UI feedback.</summary>
	public float Gain { get; }

	/// <summary>Monotonic frame counter since stream start.</summary>
	public long FrameIndex { get; }

	/// <summary>Seconds elapsed since the previous frame.</summary>
	public float DeltaTime { get; }

	public AudioReactiveState(float[] bands, float[] bandsRaw, float bass, float lowMid, float mid,
		float highMid, float treble, float rms, float peak, float centroid, float flux,
		float beatPulse, bool beat, float bpm, float stereoBalance, float[] chroma, float gate,
		float gain, long frameIndex, float deltaTime) {
		Bands = bands;
		BandsRaw = bandsRaw;
		Bass = bass;
		LowMid = lowMid;
		Mid = mid;
		HighMid = highMid;
		Treble = treble;
		Rms = rms;
		Peak = peak;
		Centroid = centroid;
		Flux = flux;
		BeatPulse = beatPulse;
		Beat = beat;
		Bpm = bpm;
		StereoBalance = stereoBalance;
		Chroma = chroma;
		Gate = gate;
		Gain = gain;
		FrameIndex = frameIndex;
		DeltaTime = deltaTime;
	}

	/// <summary>
	///     Sample a 'progress around the band ladder' value (0..1) and read the
	///     interpolated band magnitude. Useful for effects that need a band per
	///     LED slot regardless of the configured band count.
	/// </summary>
	public float SampleBand(float t) {
		if (Bands.Length == 0) {
			return 0f;
		}

		t = Math.Clamp(t, 0f, 1f);
		var idx = t * (Bands.Length - 1);
		var i0 = (int)MathF.Floor(idx);
		var i1 = Math.Min(i0 + 1, Bands.Length - 1);
		var frac = idx - i0;
		return Bands[i0] * (1f - frac) + Bands[i1] * frac;
	}
}
