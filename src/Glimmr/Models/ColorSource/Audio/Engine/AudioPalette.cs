#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

#endregion

namespace Glimmr.Models.ColorSource.Audio.Engine;

/// <summary>
///     Named gradient palette with cubic-Hermite-ish interpolation.
///     Effects sample via <see cref="Sample" /> using a 0..1 cursor and may
///     optionally rotate / mirror it for animation.
/// </summary>
public sealed class AudioPalette {
	private static readonly Dictionary<string, AudioPalette> BuiltIns = BuildBuiltIns();

	private readonly Color[] _stops;
	private readonly float[] _positions;

	public string Name { get; }

	public AudioPalette(string name, IList<Color> stops, IList<float>? positions = null) {
		Name = name;
		if (stops == null || stops.Count == 0) {
			_stops = new[] { Color.Black, Color.White };
			_positions = new[] { 0f, 1f };
			return;
		}

		_stops = new Color[stops.Count];
		_positions = new float[stops.Count];
		for (var i = 0; i < stops.Count; i++) {
			_stops[i] = stops[i];
			_positions[i] = positions != null && i < positions.Count
				? Math.Clamp(positions[i], 0f, 1f)
				: stops.Count == 1 ? 0f : i / (float)(stops.Count - 1);
		}
	}

	public Color Sample(float t) {
		if (_stops.Length == 1) {
			return _stops[0];
		}

		t -= MathF.Floor(t); // wrap into 0..1
		if (t < 0f) {
			t += 1f;
		}

		for (var i = 0; i < _stops.Length - 1; i++) {
			var p0 = _positions[i];
			var p1 = _positions[i + 1];
			if (t < p0 || t > p1) {
				continue;
			}

			var span = MathF.Max(1e-6f, p1 - p0);
			var local = (t - p0) / span;
			return Lerp(_stops[i], _stops[i + 1], local);
		}

		return _stops[^1];
	}

	public static AudioPalette Get(string? name) {
		if (string.IsNullOrWhiteSpace(name)) {
			return BuiltIns["aurora"];
		}

		var key = name.Trim().ToLowerInvariant();
		if (BuiltIns.TryGetValue(key, out var p)) {
			return p;
		}

		// Allow inline hex stops separated by commas, e.g. "#102030,#ffaa00".
		try {
			var tokens = key.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
			if (tokens.Length >= 2) {
				var stops = new List<Color>();
				foreach (var token in tokens) {
					var hex = token.Trim();
					if (hex.StartsWith('#')) {
						hex = hex[1..];
					}

					if (hex.Length == 6 &&
					    int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture,
						    out var rgb)) {
						stops.Add(Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF,
							rgb & 0xFF));
					}
				}

				if (stops.Count >= 2) {
					return new AudioPalette(name!, stops);
				}
			}
		} catch {
			// fall through to default
		}

		return BuiltIns["aurora"];
	}

	private static Color Lerp(Color a, Color b, float t) {
		t = Math.Clamp(t, 0f, 1f);
		return Color.FromArgb(
			255,
			(int)(a.R + (b.R - a.R) * t),
			(int)(a.G + (b.G - a.G) * t),
			(int)(a.B + (b.B - a.B) * t));
	}

	private static Dictionary<string, AudioPalette> BuildBuiltIns() {
		var result = new Dictionary<string, AudioPalette>(StringComparer.OrdinalIgnoreCase);

		void Add(string name, params (int r, int g, int b)[] stops) {
			var colors = new Color[stops.Length];
			for (var i = 0; i < stops.Length; i++) {
				colors[i] = Color.FromArgb(255, stops[i].r, stops[i].g, stops[i].b);
			}

			result[name] = new AudioPalette(name, colors);
		}

		Add("aurora", (5, 10, 30), (40, 0, 90), (0, 180, 220), (80, 255, 160), (255, 80, 200), (5, 10, 30));
		Add("fire", (5, 0, 0), (60, 0, 0), (180, 30, 0), (255, 140, 0), (255, 240, 120), (255, 255, 255));
		Add("ice", (0, 0, 20), (0, 60, 120), (60, 180, 220), (200, 240, 255), (255, 255, 255));
		Add("plasma", (10, 0, 30), (90, 0, 130), (220, 0, 130), (255, 90, 0), (255, 220, 0));
		Add("neon", (0, 0, 0), (255, 0, 200), (0, 255, 255), (180, 255, 0), (255, 100, 0), (0, 0, 0));
		Add("sunset", (10, 0, 40), (60, 0, 80), (200, 40, 100), (255, 130, 60), (255, 220, 120));
		Add("rainbow", (255, 0, 0), (255, 120, 0), (255, 240, 0), (60, 220, 0), (0, 120, 255),
			(120, 0, 200), (255, 0, 0));
		Add("forest", (5, 20, 10), (30, 80, 30), (120, 200, 80), (255, 240, 120), (200, 80, 30));
		Add("oceanic", (0, 5, 20), (0, 60, 120), (0, 180, 200), (120, 240, 220), (255, 255, 255));
		Add("midnight", (5, 0, 30), (30, 0, 90), (120, 30, 200), (255, 80, 200), (255, 220, 120));
		Add("hellfire", (10, 0, 0), (160, 0, 0), (255, 60, 0), (255, 200, 0), (255, 255, 200));
		Add("synthwave", (15, 0, 60), (255, 0, 180), (0, 220, 255), (255, 255, 100));

		return result;
	}
}
