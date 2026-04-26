#region

using System;
using System.Drawing;
using Glimmr.Models.Util;

#endregion

namespace Glimmr.Models.ColorSource.Audio.Engine.Effects;

/// <summary>
///     Smooth gradient that drifts around the perimeter; centroid drives
///     hue, RMS drives saturation, beats trigger a soft bloom that rolls
///     once around the TV.
/// </summary>
public sealed class AuroraFlowEffect : IAudioEffect {
	private float _bloomPhase;
	private float _bloomStrength;
	private float _phase;

	public string Name => "Aurora";

	public void Reset() {
		_phase = 0f;
		_bloomPhase = 0f;
		_bloomStrength = 0f;
	}

	public void Render(AudioReactiveState state, AudioScene scene, Color[] output) {
		var palette = AudioPalette.Get(scene.Palette);
		var motion = scene.Motion <= 0f ? 0.05f : scene.Motion;
		_phase += state.DeltaTime * (0.05f + motion * 0.6f) * (0.4f + state.Centroid);
		if (_phase > 1f) {
			_phase -= 1f;
		}

		// Beat triggers a 'comet' that travels once around the perimeter.
		if (state.Beat) {
			_bloomStrength = 1f;
			_bloomPhase = state.StereoBalance * 0.5f + 0.5f;
		}

		_bloomPhase += state.DeltaTime * (0.7f + motion * 1.4f);
		if (_bloomPhase > 1f) {
			_bloomPhase -= 1f;
		}

		_bloomStrength = MathF.Max(0f, _bloomStrength - state.DeltaTime * 1.5f);

		var sat = 0.55f + state.Rms * 0.45f;
		var energy = 0.25f + state.Rms * 0.75f + state.BeatPulse * 0.25f;
		energy = MathF.Min(1f, energy);

		for (var i = 0; i < PerimeterLayout.SlotCount; i++) {
			var theta = (i + 0.5f) / PerimeterLayout.SlotCount;
			var t = theta + _phase;
			var baseColor = palette.Sample(t);

			var slotEnergy = state.SampleBand(theta);
			var bloomWeight = PerimeterLayout.SlotWeight(i, _bloomPhase, 4f) * _bloomStrength;

			var v = energy * (0.5f + slotEnergy * 0.5f) + bloomWeight * 0.6f;
			v = Math.Clamp(v, 0f, 1f);

			output[i] = MixToHsv(baseColor, sat, v);
		}
	}

	private static Color MixToHsv(Color baseColor, double saturation, double value) {
		var (h, _, _) = RgbToHsv(baseColor);
		return ColorUtil.HsvToColor(h, saturation, value);
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
