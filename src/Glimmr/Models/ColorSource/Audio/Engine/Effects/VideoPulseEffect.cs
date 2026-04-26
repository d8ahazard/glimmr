#region

using System;
using System.Drawing;

#endregion

namespace Glimmr.Models.ColorSource.Audio.Engine.Effects;

/// <summary>
///     Audio + video friendly effect: produces an 'audio mask' where the
///     <em>brightness</em> of each slot is what matters, with subtle hue
///     hints layered on. Used by AudioVideo mode and as a calm option for
///     the audio-only path.
/// </summary>
public sealed class VideoPulseEffect : IAudioEffect {
	public string Name => "VideoPulse";

	public void Reset() { }

	public void Render(AudioReactiveState state, AudioScene scene, Color[] output) {
		var palette = AudioPalette.Get(scene.Palette);

		var bottomBoost = state.Bass * 0.7f + state.BeatPulse * 0.4f;
		var topBoost = state.Treble * 0.5f + state.HighMid * 0.3f;

		for (var i = 0; i < PerimeterLayout.SlotCount; i++) {
			var theta = (i + 0.5f) / PerimeterLayout.SlotCount;
			var bandEnergy = state.SampleBand(theta);

			float boost;
			if (i >= PerimeterLayout.BottomStart) {
				boost = bottomBoost;
			} else if (i is >= PerimeterLayout.TopStart and < PerimeterLayout.LeftStart) {
				boost = topBoost;
			} else {
				boost = state.Mid * 0.5f;
			}

			var v = Math.Clamp(state.Rms * 0.5f + bandEnergy * 0.45f + boost * 0.45f, 0f, 1f);
			var hueColor = palette.Sample(state.Centroid + theta * 0.2f);
			byte r = (byte)Math.Clamp(hueColor.R * v, 0, 255);
			byte g = (byte)Math.Clamp(hueColor.G * v, 0, 255);
			byte b = (byte)Math.Clamp(hueColor.B * v, 0, 255);
			output[i] = Color.FromArgb(255, r, g, b);
		}
	}
}
