#region

using System;
using System.Drawing;

#endregion

namespace Glimmr.Models.ColorSource.Audio.Engine.Effects;

/// <summary>
///     Bottom and corners ride the bass envelope and bloom outward from
///     the center on every kick. Treble adds restrained sparkle on the
///     top edge so vocals/cymbals still register without competing with
///     the kick drum.
/// </summary>
public sealed class BassCannonEffect : IAudioEffect {
	private readonly float[] _decay = new float[PerimeterLayout.SlotCount];
	private float _shockwave;
	private float _shockwavePhase;

	public string Name => "BassCannon";

	public void Reset() {
		Array.Clear(_decay, 0, _decay.Length);
		_shockwave = 0f;
		_shockwavePhase = 0f;
	}

	public void Render(AudioReactiveState state, AudioScene scene, Color[] output) {
		var palette = AudioPalette.Get(scene.Palette);
		var sparkle = Math.Clamp(scene.Sparkle, 0f, 1f);
		var motion = scene.Motion <= 0f ? 0.5f : scene.Motion;

		if (state.Beat && state.Bass > 0.15f) {
			_shockwave = 1f;
			_shockwavePhase = 0f;
		}

		_shockwavePhase += state.DeltaTime * (1.5f + motion * 2.5f);
		_shockwave = MathF.Max(0f, _shockwave - state.DeltaTime * 1.3f);

		// Decay envelope per slot for trailing afterglow.
		for (var i = 0; i < _decay.Length; i++) {
			_decay[i] = MathF.Max(0f, _decay[i] - state.DeltaTime * 1.5f);
		}

		// Bottom slots ride the bass directly.
		for (var i = PerimeterLayout.BottomStart;
		     i < PerimeterLayout.BottomStart + PerimeterLayout.BottomLength;
		     i++) {
			var v = state.Bass * (0.7f + state.BeatPulse * 0.3f);
			if (v > _decay[i]) {
				_decay[i] = v;
			}
		}

		// Corners get the punchy mid-bass snap.
		var corners = new[] {
			PerimeterLayout.RightStart,
			PerimeterLayout.RightStart + PerimeterLayout.RightLength - 1,
			PerimeterLayout.LeftStart,
			PerimeterLayout.LeftStart + PerimeterLayout.LeftLength - 1,
			PerimeterLayout.BottomStart,
			PerimeterLayout.BottomStart + PerimeterLayout.BottomLength - 1
		};
		foreach (var c in corners) {
			var v = state.LowMid * (0.5f + state.BeatPulse * 0.5f);
			if (v > _decay[c]) {
				_decay[c] = v;
			}
		}

		// Treble sparkle along the top.
		for (var i = PerimeterLayout.TopStart;
		     i < PerimeterLayout.TopStart + PerimeterLayout.TopLength;
		     i++) {
			var theta = PerimeterLayout.ThetaForSlot(i);
			var bandEnergy = state.SampleBand(0.6f + (theta - 0.4f) * 0.5f);
			var v = state.Treble * 0.6f + bandEnergy * 0.4f;
			v *= 0.4f + sparkle * 0.8f;
			if (v > _decay[i]) {
				_decay[i] = v;
			}
		}

		// Shockwave: when fired, a band of bright energy sweeps outward in
		// both directions from the bottom-center.
		if (_shockwave > 0f) {
			var spread = _shockwavePhase * PerimeterLayout.SlotCount * 0.5f;
			var bottomCenter = PerimeterLayout.BottomStart + PerimeterLayout.BottomLength * 0.5f;
			for (var i = 0; i < PerimeterLayout.SlotCount; i++) {
				var dist = MathF.Abs(i - bottomCenter);
				var ringDist = MathF.Abs(dist - spread);
				if (ringDist < 1.5f) {
					var w = (1.5f - ringDist) / 1.5f * _shockwave;
					if (w > _decay[i]) {
						_decay[i] = w;
					}
				}
			}
		}

		for (var i = 0; i < PerimeterLayout.SlotCount; i++) {
			var t = state.Centroid + i * 0.04f;
			var baseColor = palette.Sample(t);
			var v = _decay[i];
			output[i] = ColorScale(baseColor, v);
		}
	}

	private static Color ColorScale(Color c, float v) {
		v = Math.Clamp(v, 0f, 1f);
		return Color.FromArgb(255, (int)(c.R * v), (int)(c.G * v), (int)(c.B * v));
	}
}
