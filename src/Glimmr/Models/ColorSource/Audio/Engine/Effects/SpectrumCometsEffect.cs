#region

using System;
using System.Drawing;

#endregion

namespace Glimmr.Models.ColorSource.Audio.Engine.Effects;

/// <summary>
///     Each perimeter slot corresponds to a frequency band. Bands are
///     persisted with peak-hold + decay so high-energy hits leave glowing
///     trails that fall off independently of their neighbours.
/// </summary>
public sealed class SpectrumCometsEffect : IAudioEffect {
	private readonly float[] _decay = new float[PerimeterLayout.SlotCount];
	private readonly float[] _peakHold = new float[PerimeterLayout.SlotCount];

	public string Name => "SpectrumComets";

	public void Reset() {
		Array.Clear(_decay, 0, _decay.Length);
		Array.Clear(_peakHold, 0, _peakHold.Length);
	}

	public void Render(AudioReactiveState state, AudioScene scene, Color[] output) {
		var palette = AudioPalette.Get(scene.Palette);
		var motion = scene.Motion <= 0f ? 0.4f : scene.Motion;
		var fall = state.DeltaTime * (0.7f + (1f - motion) * 0.8f);
		var peakFall = state.DeltaTime * 0.35f;

		for (var i = 0; i < PerimeterLayout.SlotCount; i++) {
			var theta = (i + 0.5f) / PerimeterLayout.SlotCount;
			var v = state.SampleBand(theta);

			// Energy fills from the bottom-up: comet body.
			if (v > _decay[i]) {
				_decay[i] = v;
			} else {
				_decay[i] = MathF.Max(0f, _decay[i] - fall);
			}

			// Peak-hold dot.
			if (_decay[i] > _peakHold[i]) {
				_peakHold[i] = _decay[i];
			} else {
				_peakHold[i] = MathF.Max(0f, _peakHold[i] - peakFall);
			}

			var t = state.Centroid * 0.4f + theta * 0.6f;
			var baseColor = palette.Sample(t);
			var peakColor = palette.Sample(t + 0.5f);

			var b = _decay[i];
			var p = _peakHold[i];

			byte r = (byte)Math.Clamp(baseColor.R * b + peakColor.R * (p - b) * 1.4f, 0, 255);
			byte g = (byte)Math.Clamp(baseColor.G * b + peakColor.G * (p - b) * 1.4f, 0, 255);
			byte bl = (byte)Math.Clamp(baseColor.B * b + peakColor.B * (p - b) * 1.4f, 0, 255);

			output[i] = Color.FromArgb(255, r, g, bl);
		}
	}
}
