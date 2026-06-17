#region

using System;
using System.Collections.Generic;
using System.Drawing;

#endregion

namespace Glimmr.Models.ColorSource.Scene.Script.Providers;

/// <summary>
///     Smooth layered-sine "plasma" / aurora field. Three drifting sine waves combine into a
///     slowly-evolving hue field - calm, ambient, and great on a matrix. Params: speed (drift
///     rate), scale (spatial frequency), hue (palette center, degrees).
/// </summary>
public sealed class AuroraProvider : ISceneProvider {
	private readonly double _speed;
	private readonly double _scale;
	private readonly double _hueBase;
	private double _t;

	public AuroraProvider(int width, int height, IReadOnlyDictionary<string, double> p) {
		Width = Math.Max(1, width);
		Height = Math.Max(1, height);
		_speed = p.Get("speed", 1.0);
		_scale = Math.Max(0.05, p.Get("scale", 1.0));
		_hueBase = p.Get("hue", 150);
	}

	public int Width { get; }
	public int Height { get; }
	public double Fps => 30;

	public void NextFrame(Color[] grid) {
		_t += _speed / Fps;
		var fx = 0.18 * _scale;
		var fy = 0.13 * _scale;
		for (var y = 0; y < Height; y++) {
			for (var x = 0; x < Width; x++) {
				var v = Math.Sin(x * fx + _t)
				        + Math.Sin(y * fy - _t * 0.8)
				        + Math.Sin((x + y) * fx * 0.5 + _t * 1.3)
				        + Math.Sin(Math.Sqrt(x * x + y * y) * fy + _t * 0.6);
				// v is in [-4,4]; map to a hue swing around the base and a gentle brightness ripple.
				var hue = _hueBase + v * 40;
				var bright = 0.55 + 0.45 * (v / 4.0);
				grid[y * Width + x] = SceneProviderUtil.Hsv(hue, 0.85, Math.Clamp(bright, 0.1, 1));
			}
		}
	}
}
