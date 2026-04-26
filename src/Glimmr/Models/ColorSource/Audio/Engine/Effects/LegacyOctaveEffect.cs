#region

using System.Collections.Generic;
using System.Drawing;
using Glimmr.Models.Util;

#endregion

namespace Glimmr.Models.ColorSource.Audio.Engine.Effects;

/// <summary>
///     Backwards-compatible renderer that reproduces the original
///     'highest-note-per-octave + hue rotation' visualizer. Used when a
///     scene is selected that only carries an OctaveMap and no Mode (i.e.
///     scenes from before this engine existed).
/// </summary>
public sealed class LegacyOctaveEffect : IAudioEffect {
	private static readonly int[] LeftSectors = { 16, 15, 14, 13, 12, 11, 10, 9, 8, 7 };
	private static readonly int[] RightSectors = { 17, 18, 19, 0, 1, 2, 3, 4, 5, 6 };

	private static readonly float[] OctaveStart =
		{ 16.35f, 32.7f, 65.41f, 130.81f, 261.63f, 523.25f, 1046.5f, 2093f, 4186.01f };

	private static readonly float[] OctaveEnd =
		{ 30.87f, 61.74f, 123.47f, 246.94f, 493.88f, 987.77f, 1975.53f, 3951.07f, 7902.13f };

	private float _rotation;
	private bool _triggered;
	private int _kickCount;

	public string Name => "Legacy";

	public void Reset() {
		_rotation = 0f;
		_triggered = false;
		_kickCount = 0;
	}

	public void Render(AudioReactiveState state, AudioScene scene, Color[] output) {
		for (var i = 0; i < output.Length; i++) {
			output[i] = Color.FromArgb(0, 0, 0, 0);
		}

		if (scene.OctaveMap == null || scene.OctaveMap.Count == 0) {
			return;
		}

		// Reconstruct an approximate freq->amp dictionary out of the band
		// data we have, using band centers as the 'frequency'. This is good
		// enough to drive HighNote in the legacy spirit.
		var freqAmp = new Dictionary<float, int>(state.BandsRaw.Length);
		for (var i = 0; i < state.BandsRaw.Length; i++) {
			var amp = (int)(state.Bands[i] * 255f);
			if (amp <= 0) {
				continue;
			}

			freqAmp[CenterFor(state, i)] = amp;
		}

		var triggered = false;
		var rotationLower = scene.RotationLower;
		var rotationUpper = scene.RotationUpper <= rotationLower ? 1f : scene.RotationUpper;
		var rotationSpeed = scene.RotationSpeed;
		var rotationThreshold = scene.RotationThreshold > 1f
			? scene.RotationThreshold / 255f
			: scene.RotationThreshold;

		foreach (var (key, octave) in scene.OctaveMap) {
			if (!int.TryParse(key, out var region) || region < 0 || region >= LeftSectors.Length) {
				continue;
			}

			if (octave < 0 || octave >= OctaveStart.Length) {
				continue;
			}

			var (freq, amp) = HighNote(freqAmp, octave);
			if (amp == 0) {
				continue;
			}

			if (!triggered && freq < 100f && amp >= rotationThreshold * 255f) {
				triggered = true;
			}

			var hue = ColorUtil.HueFromFrequency(freq, octave);
			var range = rotationUpper - rotationLower;
			hue = rotationLower + range * hue + _rotation;
			while (hue > 1f) {
				hue -= 1f;
			}

			while (hue < 0f) {
				hue += 1f;
			}

			var color = ColorUtil.HsvToColor(hue * 360, 1, amp / 255f);
			output[LeftSectors[region]] = color;
			output[RightSectors[region]] = color;
		}

		if (triggered) {
			_kickCount++;
		}

		if (_kickCount >= 4) {
			_kickCount = 0;
			_triggered = true;
		}

		if (_triggered) {
			_triggered = false;
			_rotation += rotationSpeed;
			while (_rotation > 1f) {
				_rotation -= 1f;
			}

			while (_rotation < 0f) {
				_rotation += 1f;
			}
		}
	}

	private static float CenterFor(AudioReactiveState state, int idx) {
		// Spread the band centers across the audible range using log-2 spacing.
		var t = (idx + 0.5f) / state.BandsRaw.Length;
		return 30f * (float)System.Math.Pow(16000f / 30f, t);
	}

	private static (float freq, int amp) HighNote(Dictionary<float, int> freqAmp, int octave) {
		var minF = OctaveStart[octave];
		var maxF = OctaveEnd[octave];
		var amp = 0;
		var freq = 0f;
		foreach (var kvp in freqAmp) {
			if (kvp.Key < minF || kvp.Key > maxF) {
				continue;
			}

			if (kvp.Value <= amp) {
				continue;
			}

			amp = kvp.Value;
			freq = kvp.Key;
		}

		return (freq, amp);
	}
}
