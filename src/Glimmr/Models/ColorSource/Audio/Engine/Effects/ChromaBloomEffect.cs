#region

using System;
using System.Drawing;
using Glimmr.Models.Util;

#endregion

namespace Glimmr.Models.ColorSource.Audio.Engine.Effects;

/// <summary>
///     Maps the 12 pitch classes onto a hue wheel and blooms each one
///     proportional to its current energy. Beat pulses scale brightness;
///     spectral flux controls how much "spread" each pitch class gets
///     around the perimeter.
/// </summary>
public sealed class ChromaBloomEffect : IAudioEffect {
	private float _spread;

	public string Name => "ChromaBloom";

	public void Reset() {
		_spread = 0f;
	}

	public void Render(AudioReactiveState state, AudioScene scene, Color[] output) {
		var palette = AudioPalette.Get(scene.Palette);
		var motion = scene.Motion <= 0f ? 0.4f : scene.Motion;
		_spread += (state.Flux - _spread) * Math.Min(1f, state.DeltaTime * 4f);

		var beat = MathF.Min(1f, state.BeatPulse + state.Bass * 0.3f);
		var energy = 0.25f + state.Rms * 0.6f + beat * 0.25f;
		energy = MathF.Min(1f, energy);

		// Find dominant chroma pitch class for hue rotation.
		var dom = 0;
		var domV = 0f;
		for (var c = 0; c < state.Chroma.Length; c++) {
			if (state.Chroma[c] > domV) {
				domV = state.Chroma[c];
				dom = c;
			}
		}

		var rotation = dom / 12f + state.Centroid * 0.2f + state.FrameIndex * motion * 0.0015f;

		for (var i = 0; i < PerimeterLayout.SlotCount; i++) {
			var theta = (i + 0.5f) / PerimeterLayout.SlotCount;
			var paletteT = theta + rotation;
			var baseColor = palette.Sample(paletteT);

			// Mix in the matching chroma cell so the dominant note "blooms"
			// out from its assigned arc of the perimeter.
			var chromaIdx = (int)MathF.Floor(theta * 12) % 12;
			var cellEnergy = state.Chroma.Length > 0 ? state.Chroma[chromaIdx] : 0f;
			var spreadGlow = MathF.Min(1f,
				cellEnergy * (0.5f + _spread * 1.5f) + state.SampleBand(theta) * 0.4f);

			var v = energy * (0.55f + spreadGlow * 0.55f);
			v = Math.Clamp(v, 0f, 1f);

			var (h, s, _) = RgbToHsv(baseColor);
			output[i] = ColorUtil.HsvToColor(h, MathF.Min(1f, (float)s + 0.2f), v);
		}
	}

	private static (double h, double s, double v) RgbToHsv(Color c) {
		double r = c.R / 255.0;
		double g = c.G / 255.0;
		double b = c.B / 255.0;
		var max = Math.Max(r, Math.Max(g, b));
		var min = Math.Min(r, Math.Min(g, b));
		var d = max - min;
		double h = 0;
		if (d > 0.0001) {
			if (max == r) {
				h = 60 * (((g - b) / d) % 6);
			} else if (max == g) {
				h = 60 * ((b - r) / d + 2);
			} else {
				h = 60 * ((r - g) / d + 4);
			}
		}

		if (h < 0) {
			h += 360;
		}

		var s = max <= 0.0001 ? 0 : d / max;
		return (h, s, max);
	}
}
