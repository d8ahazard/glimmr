#region

using System;
using System.Drawing;

#endregion

namespace Glimmr.Models.ColorSource.Audio.Engine.Effects;

/// <summary>
///     Two counter-rotating waves whose speed scales with BPM and whose
///     amplitude scales with bass + flux. Produces a hypnotic 'vortex'
///     pattern that locks to musical tempo when one is detectable.
/// </summary>
public sealed class VortexEffect : IAudioEffect {
	private float _phaseA;
	private float _phaseB;

	public string Name => "Vortex";

	public void Reset() {
		_phaseA = 0f;
		_phaseB = 0.5f;
	}

	public void Render(AudioReactiveState state, AudioScene scene, Color[] output) {
		var palette = AudioPalette.Get(scene.Palette);
		var motion = scene.Motion <= 0f ? 0.5f : scene.Motion;
		var tempoFactor = state.Bpm > 30f ? state.Bpm / 120f : 1f;
		var rate = (0.4f + motion * 1.6f) * tempoFactor;

		_phaseA += state.DeltaTime * rate;
		_phaseB -= state.DeltaTime * rate * 0.7f;

		var amp = 0.4f + state.Bass * 0.6f + state.BeatPulse * 0.3f;
		amp = MathF.Min(1f, amp);

		for (var i = 0; i < PerimeterLayout.SlotCount; i++) {
			var theta = (i + 0.5f) / PerimeterLayout.SlotCount;
			// Two sine lobes counter-rotating around the perimeter.
			var a = MathF.Sin((theta + _phaseA) * MathF.PI * 4f);
			var b = MathF.Sin((theta - _phaseB) * MathF.PI * 6f);
			var v = (a + b) * 0.25f + 0.5f;
			v = Math.Clamp(v * amp + state.SampleBand(theta) * 0.4f, 0f, 1f);

			var paletteT = theta + _phaseA * 0.3f + state.Centroid * 0.4f;
			var baseColor = palette.Sample(paletteT);

			output[i] = Color.FromArgb(255,
				(byte)Math.Clamp(baseColor.R * v, 0, 255),
				(byte)Math.Clamp(baseColor.G * v, 0, 255),
				(byte)Math.Clamp(baseColor.B * v, 0, 255));
		}
	}
}
