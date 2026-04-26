#region

using System;

#endregion

namespace Glimmr.Models.ColorSource.Audio.Engine;

/// <summary>
///     Frequency-band layout used by the audio analyzer.
///     A band is a contiguous range of FFT bins that get summed/averaged
///     into a single normalized magnitude value. Choosing a perceptual
///     scale (mel/log/octave) instead of raw linear bins keeps low
///     frequencies legible without the highs dominating the picture.
/// </summary>
public sealed class AudioBandConfig {
	public enum BandScale {
		Log = 0,
		Mel = 1,
		Octave = 2
	}

	private const float MinFrequency = 30f;
	private const float MaxFrequency = 16000f;

	public int BandCount { get; }
	public BandScale Scale { get; }
	public float[] LowEdge { get; }
	public float[] HighEdge { get; }
	public float[] Center { get; }

	private AudioBandConfig(int bandCount, BandScale scale, float[] low, float[] high) {
		BandCount = bandCount;
		Scale = scale;
		LowEdge = low;
		HighEdge = high;
		Center = new float[bandCount];
		for (var i = 0; i < bandCount; i++) {
			Center[i] = (low[i] + high[i]) * 0.5f;
		}
	}

	public static AudioBandConfig Build(int bandCount, BandScale scale, float fMin = MinFrequency,
		float fMax = MaxFrequency) {
		if (bandCount < 2) {
			bandCount = 2;
		}

		if (fMin <= 0) {
			fMin = 1f;
		}

		if (fMax <= fMin) {
			fMax = fMin * 2f;
		}

		var low = new float[bandCount];
		var high = new float[bandCount];

		switch (scale) {
			case BandScale.Mel: {
				var mMin = HzToMel(fMin);
				var mMax = HzToMel(fMax);
				for (var i = 0; i < bandCount; i++) {
					var t0 = i / (float)bandCount;
					var t1 = (i + 1) / (float)bandCount;
					low[i] = MelToHz(mMin + (mMax - mMin) * t0);
					high[i] = MelToHz(mMin + (mMax - mMin) * t1);
				}

				break;
			}
			case BandScale.Octave: {
				var octaves = MathF.Log2(fMax / fMin);
				for (var i = 0; i < bandCount; i++) {
					low[i] = fMin * MathF.Pow(2f, octaves * i / bandCount);
					high[i] = fMin * MathF.Pow(2f, octaves * (i + 1) / bandCount);
				}

				break;
			}
			default: {
				var lMin = MathF.Log10(fMin);
				var lMax = MathF.Log10(fMax);
				for (var i = 0; i < bandCount; i++) {
					var t0 = i / (float)bandCount;
					var t1 = (i + 1) / (float)bandCount;
					low[i] = MathF.Pow(10f, lMin + (lMax - lMin) * t0);
					high[i] = MathF.Pow(10f, lMin + (lMax - lMin) * t1);
				}

				break;
			}
		}

		return new AudioBandConfig(bandCount, scale, low, high);
	}

	private static float HzToMel(float hz) {
		return 2595f * MathF.Log10(1f + hz / 700f);
	}

	private static float MelToHz(float mel) {
		return 700f * (MathF.Pow(10f, mel / 2595f) - 1f);
	}
}
