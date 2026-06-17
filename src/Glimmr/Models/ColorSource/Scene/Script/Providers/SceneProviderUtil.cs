#region

using System;
using System.Collections.Generic;
using System.Drawing;

#endregion

namespace Glimmr.Models.ColorSource.Scene.Script.Providers;

/// <summary>
///     Shared helpers for the built-in (non-script) animation providers: reading slider values
///     with a fallback, and HSV-&gt;RGB conversion for vivid, easily-shifted colors.
/// </summary>
internal static class SceneProviderUtil {
	public static double Get(this IReadOnlyDictionary<string, double> values, string key, double fallback) {
		return values.TryGetValue(key, out var v) ? v : fallback;
	}

	public static int GetInt(this IReadOnlyDictionary<string, double> values, string key, int fallback) {
		return values.TryGetValue(key, out var v) ? (int)Math.Round(v) : fallback;
	}

	/// <summary>HSV (h in degrees, s/v in 0..1) to an RGB <see cref="Color" />.</summary>
	public static Color Hsv(double h, double s, double v) {
		h = ((h % 360) + 360) % 360;
		s = Math.Clamp(s, 0, 1);
		v = Math.Clamp(v, 0, 1);
		var c = v * s;
		var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
		var m = v - c;
		double r = 0, g = 0, b = 0;
		switch ((int)(h / 60)) {
			case 0: r = c; g = x; break;
			case 1: r = x; g = c; break;
			case 2: g = c; b = x; break;
			case 3: g = x; b = c; break;
			case 4: r = x; b = c; break;
			default: r = c; b = x; break;
		}

		return Color.FromArgb(
			(int)Math.Round((r + m) * 255),
			(int)Math.Round((g + m) * 255),
			(int)Math.Round((b + m) * 255));
	}

	/// <summary>Scale an RGB color by a 0..1 brightness factor.</summary>
	public static Color Scale(Color c, double f) {
		f = Math.Clamp(f, 0, 1);
		return Color.FromArgb((int)(c.R * f), (int)(c.G * f), (int)(c.B * f));
	}

	/// <summary>Clear a grid to black.</summary>
	public static void Clear(Color[] grid) {
		for (var i = 0; i < grid.Length; i++) {
			grid[i] = Color.Black;
		}
	}
}
