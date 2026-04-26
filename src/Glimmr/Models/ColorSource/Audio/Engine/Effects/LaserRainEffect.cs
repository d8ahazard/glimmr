#region

using System;
using System.Drawing;

#endregion

namespace Glimmr.Models.ColorSource.Audio.Engine.Effects;

/// <summary>
///     Treble-heavy effect: hi-hats / vocals trigger fast laser streaks
///     around the perimeter. Includes a strobe limiter to keep this from
///     becoming an actual hazard at high beat sensitivity.
/// </summary>
public sealed class LaserRainEffect : IAudioEffect {
	private const int MaxStreaks = 12;

	private readonly Streak[] _streaks = new Streak[MaxStreaks];
	private float _flashCooldown;
	private float _flashEnvelope;
	private int _next;
	private float _phase;

	public string Name => "LaserRain";

	public void Reset() {
		for (var i = 0; i < _streaks.Length; i++) {
			_streaks[i] = default;
		}

		_phase = 0f;
		_flashEnvelope = 0f;
		_flashCooldown = 0f;
	}

	public void Render(AudioReactiveState state, AudioScene scene, Color[] output) {
		var palette = AudioPalette.Get(scene.Palette);
		var strobeLimit = Math.Clamp(scene.StrobeLimit, 0f, 1f);
		var motion = scene.Motion <= 0f ? 0.6f : scene.Motion;
		_phase += state.DeltaTime * 0.4f;

		// Spawn streaks from highmid + treble onsets.
		var spawnEnergy = MathF.Max(state.HighMid, state.Treble * 1.3f);
		if (spawnEnergy > 0.18f && state.Flux > 0.18f) {
			SpawnStreak(state, palette, spawnEnergy, motion);
		}

		// Beat-driven extra spawn so we get visible streaks even on quiet hats.
		if (state.Beat) {
			SpawnStreak(state, palette, 0.7f, motion);
		}

		// Update streaks.
		for (var i = 0; i < _streaks.Length; i++) {
			ref var s = ref _streaks[i];
			if (s.Life <= 0f) {
				continue;
			}

			s.Position += state.DeltaTime * s.Velocity;
			s.Position -= MathF.Floor(s.Position);
			s.Life -= state.DeltaTime * (0.8f + motion * 0.7f);
		}

		// Beat flash, throttled by strobe limit.
		_flashEnvelope = MathF.Max(0f, _flashEnvelope - state.DeltaTime * 6f);
		_flashCooldown = MathF.Max(0f, _flashCooldown - state.DeltaTime);
		if (state.Beat && _flashCooldown <= 0f) {
			var max = 1f - strobeLimit * 0.85f;
			_flashEnvelope = max;
			_flashCooldown = 0.05f + strobeLimit * 0.5f;
		}

		for (var i = 0; i < PerimeterLayout.SlotCount; i++) {
			var theta = (i + 0.5f) / PerimeterLayout.SlotCount;
			var v = 0f;
			var weighted = (r: 0f, g: 0f, b: 0f);

			for (var s = 0; s < _streaks.Length; s++) {
				var streak = _streaks[s];
				if (streak.Life <= 0f) {
					continue;
				}

				var w = PerimeterLayout.SlotWeight(i, streak.Position, 1.5f) * streak.Life *
				        streak.Brightness;
				if (w <= 0f) {
					continue;
				}

				v += w;
				weighted.r += streak.Color.R / 255f * w;
				weighted.g += streak.Color.G / 255f * w;
				weighted.b += streak.Color.B / 255f * w;
			}

			// Background: subtle treble shimmer so we are not pure black.
			var ambient = state.Treble * 0.18f + state.SampleBand(theta) * 0.15f;
			v += ambient;

			// Beat flash adds a uniform white-ish wash.
			var flash = _flashEnvelope * (1f - strobeLimit);
			v = MathF.Min(1f, v + flash);

			byte r, g, b;
			if (v <= 0.0001f) {
				r = g = b = 0;
			} else {
				var br = MathF.Min(1f, v) / MathF.Max(v, 1e-3f);
				r = (byte)Math.Clamp(weighted.r * 255 * br + flash * 220, 0, 255);
				g = (byte)Math.Clamp(weighted.g * 255 * br + flash * 240, 0, 255);
				b = (byte)Math.Clamp(weighted.b * 255 * br + flash * 255, 0, 255);
			}

			output[i] = Color.FromArgb(255, r, g, b);
		}
	}

	private void SpawnStreak(AudioReactiveState state, AudioPalette palette, float strength, float motion) {
		var idx = _next % _streaks.Length;
		_next++;
		var direction = (state.FrameIndex + _next) % 2 == 0 ? 1f : -1f;
		var paletteT = state.Centroid + state.Chroma.Length > 0
			? state.Chroma[(int)(state.FrameIndex % state.Chroma.Length)]
			: 0.5f;
		_streaks[idx] = new Streak {
			Position = state.StereoBalance * 0.5f + 0.5f + (idx % 5) * 0.07f,
			Velocity = direction * (1.0f + motion * 1.5f + state.Treble * 1.2f),
			Life = 0.7f + strength * 0.6f,
			Brightness = 0.5f + strength * 0.6f,
			Color = palette.Sample(paletteT)
		};
	}

	private struct Streak {
		public float Position;
		public float Velocity;
		public float Life;
		public float Brightness;
		public Color Color;
	}
}
