#region

using System;
using System.Collections.Generic;
using System.Drawing;

#endregion

namespace Glimmr.Models.ColorSource.Scene.Script.Providers;

/// <summary>
///     Lightning over a dark stormy sky. Periodically strikes a jagged bolt (random walk from top
///     to bottom) with forked branches, plus a full-frame flash; everything decays on a light
///     field for the afterglow. Params: frequency (strikes/sec-ish), branchiness (fork chance),
///     brightness (overall intensity).
/// </summary>
public sealed class LightningProvider : ISceneProvider {
	private readonly Random _rand = new();
	private readonly int _w;
	private readonly int _h;
	private readonly float[] _field; // [w*h*3] glowing light, decays each frame
	private readonly double _frequency;
	private readonly double _branch;
	private readonly double _brightness;

	private double _nextStrike;
	private float _flash;

	public LightningProvider(int width, int height, IReadOnlyDictionary<string, double> p) {
		_w = Math.Max(2, width);
		_h = Math.Max(2, height);
		_field = new float[_w * _h * 3];
		_frequency = Math.Clamp(p.Get("frequency", 0.6), 0.05, 5);
		_branch = Math.Clamp(p.Get("branchiness", 0.5), 0, 1);
		_brightness = Math.Clamp(p.Get("brightness", 1.0), 0.2, 1.5);
		_nextStrike = ScheduleNext();
	}

	public int Width => _w;
	public int Height => _h;
	public double Fps => 30;

	private double ScheduleNext() {
		// Mean gap = 1/frequency seconds, with a randomized spread for natural timing.
		var mean = 1.0 / _frequency;
		return mean * (0.4 + _rand.NextDouble() * 1.2);
	}

	public void NextFrame(Color[] grid) {
		var dt = 1.0 / Fps;
		// Decay the glow and the sky flash.
		for (var i = 0; i < _field.Length; i++) {
			_field[i] *= 0.78f;
		}

		_flash *= 0.80f;

		_nextStrike -= dt;
		if (_nextStrike <= 0) {
			Strike();
			_nextStrike = ScheduleNext();
		}

		var sky = (int)(_flash * 40);
		for (var i = 0; i < _w * _h; i++) {
			var j = i * 3;
			// Cool dark-blue sky base + flash, plus the bolt glow on top.
			var r = 4 + sky / 2 + (int)_field[j];
			var g = 6 + sky / 2 + (int)_field[j + 1];
			var b = 16 + sky + (int)_field[j + 2];
			grid[i] = Color.FromArgb(Math.Min(255, r), Math.Min(255, g), Math.Min(255, b));
		}
	}

	private void Strike() {
		_flash = (float)(0.6 + _rand.NextDouble() * 0.4);
		var x = _rand.Next(_w);
		DrawBolt(x, 0, 0);
	}

	private void DrawBolt(int x, int y, int depth) {
		if (depth > 4) {
			return;
		}

		// Bluish-white core; deeper branches are dimmer.
		var core = SceneProviderUtil.Hsv(220, 0.25, _brightness);
		var branchBright = (float)(_brightness * Math.Pow(0.6, depth));
		while (y < _h) {
			DepositGlow(x, y, core, branchBright);
			// Step downward with a horizontal jitter.
			x += _rand.Next(-1, 2);
			x = Math.Clamp(x, 0, _w - 1);
			y++;

			// Occasionally fork a branch that veers sideways.
			if (_rand.NextDouble() < _branch * 0.18 && depth < 3) {
				var bx = Math.Clamp(x + _rand.Next(-2, 3), 0, _w - 1);
				DrawBolt(bx, y, depth + 1);
			}
		}
	}

	private void DepositGlow(int x, int y, Color c, float intensity) {
		// Bright core pixel plus a soft 1-cell halo so the bolt reads on a low-res matrix.
		AddPixel(x, y, c, intensity);
		AddPixel(x - 1, y, c, intensity * 0.4f);
		AddPixel(x + 1, y, c, intensity * 0.4f);
		AddPixel(x, y - 1, c, intensity * 0.3f);
		AddPixel(x, y + 1, c, intensity * 0.3f);
	}

	private void AddPixel(int x, int y, Color c, float intensity) {
		if (x < 0 || x >= _w || y < 0 || y >= _h) {
			return;
		}

		var idx = (y * _w + x) * 3;
		_field[idx] = Math.Min(255, _field[idx] + c.R * intensity);
		_field[idx + 1] = Math.Min(255, _field[idx + 1] + c.G * intensity);
		_field[idx + 2] = Math.Min(255, _field[idx + 2] + c.B * intensity);
	}
}
