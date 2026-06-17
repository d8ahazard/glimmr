#region

using System;
using System.Collections.Generic;
using System.Drawing;

#endregion

namespace Glimmr.Models.ColorSource.Scene.Script.Providers;

/// <summary>
///     A box of multicolored particles pulled toward the walls. Gravity activates toward up to
///     three of the four walls at once and is re-rolled every cycle, so no single direction ever
///     holds longer than the adjustable duration. Particles bounce off the walls and off each
///     other; collisions nudge their hues together and throw off a bright spark. Params: count,
///     duration (gravity switch seconds), sides (max simultaneous walls 1..3), bounciness, glow.
/// </summary>
public sealed class GravityBoxProvider : ISceneProvider {
	private readonly Random _rand = new();
	private readonly int _w;
	private readonly int _h;
	private readonly float[] _field; // [w*h*3] light field for trails + collision sparks
	private readonly Particle[] _p;

	private readonly double _duration;
	private readonly int _maxSides;
	private readonly double _restitution;
	private readonly double _glow;
	private const double Gravity = 36; // cells/sec^2 toward each active wall

	private double _ax;
	private double _ay;
	private double _switchTimer;

	public GravityBoxProvider(int width, int height, IReadOnlyDictionary<string, double> p) {
		_w = Math.Max(4, width);
		_h = Math.Max(4, height);
		_field = new float[_w * _h * 3];
		var count = Math.Clamp(p.GetInt("count", 36), 2, 200);
		_duration = Math.Clamp(p.Get("duration", 3.0), 0.5, 15);
		_maxSides = Math.Clamp(p.GetInt("sides", 3), 1, 3);
		_restitution = Math.Clamp(p.Get("bounciness", 0.85), 0.1, 1.0);
		_glow = Math.Clamp(p.Get("glow", 1.0), 0, 2);

		_p = new Particle[count];
		for (var i = 0; i < count; i++) {
			_p[i] = new Particle {
				X = (float)(_rand.NextDouble() * _w),
				Y = (float)(_rand.NextDouble() * _h),
				Vx = (float)((_rand.NextDouble() - 0.5) * 10),
				Vy = (float)((_rand.NextDouble() - 0.5) * 10),
				Hue = _rand.NextDouble() * 360
			};
		}

		RollGravity();
	}

	public int Width => _w;
	public int Height => _h;
	public double Fps => 30;

	private void RollGravity() {
		// Pick 1..maxSides distinct walls and sum their inward pull into one accel vector.
		var sides = new List<int> { 0, 1, 2, 3 }; // 0=left,1=right,2=top,3=bottom
		var k = 1 + _rand.Next(_maxSides);
		_ax = 0;
		_ay = 0;
		for (var i = 0; i < k && sides.Count > 0; i++) {
			var pick = _rand.Next(sides.Count);
			switch (sides[pick]) {
				case 0: _ax -= Gravity; break;
				case 1: _ax += Gravity; break;
				case 2: _ay -= Gravity; break;
				default: _ay += Gravity; break;
			}

			sides.RemoveAt(pick);
		}

		_switchTimer = _duration;
	}

	public void NextFrame(Color[] grid) {
		var dt = 1.0 / Fps;
		_switchTimer -= dt;
		if (_switchTimer <= 0) {
			RollGravity();
		}

		for (var i = 0; i < _field.Length; i++) {
			_field[i] *= 0.80f;
		}

		Integrate((float)dt);
		ResolveCollisions();

		foreach (var p in _p) {
			Deposit(p.X, p.Y, SceneProviderUtil.Hsv(p.Hue, 0.9, 1.0), 1f);
		}

		Compose(grid);
	}

	private void Integrate(float dt) {
		foreach (var p in _p) {
			p.Vx += (float)_ax * dt;
			p.Vy += (float)_ay * dt;
			p.X += p.Vx * dt;
			p.Y += p.Vy * dt;

			if (p.X < 0) {
				p.X = 0;
				p.Vx = -p.Vx * (float)_restitution;
			} else if (p.X > _w - 1) {
				p.X = _w - 1;
				p.Vx = -p.Vx * (float)_restitution;
			}

			if (p.Y < 0) {
				p.Y = 0;
				p.Vy = -p.Vy * (float)_restitution;
			} else if (p.Y > _h - 1) {
				p.Y = _h - 1;
				p.Vy = -p.Vy * (float)_restitution;
			}
		}
	}

	private void ResolveCollisions() {
		for (var i = 0; i < _p.Length; i++) {
			for (var j = i + 1; j < _p.Length; j++) {
				var a = _p[i];
				var b = _p[j];
				var dx = b.X - a.X;
				var dy = b.Y - a.Y;
				var d2 = dx * dx + dy * dy;
				if (d2 > 2.0f || d2 < 1e-4f) {
					continue;
				}

				// Swap velocities (equal-mass elastic-ish) and push apart so they don't stick.
				(a.Vx, b.Vx) = (b.Vx * (float)_restitution, a.Vx * (float)_restitution);
				(a.Vy, b.Vy) = (b.Vy * (float)_restitution, a.Vy * (float)_restitution);

				// Colors drift toward each other on contact, and throw a white spark.
				var mix = (a.Hue + b.Hue) / 2;
				a.Hue += (mix - a.Hue) * 0.25;
				b.Hue += (mix - b.Hue) * 0.25;
				var sx = (a.X + b.X) / 2;
				var sy = (a.Y + b.Y) / 2;
				Deposit(sx, sy, Color.White, (float)(1.5 * _glow));
			}
		}
	}

	private void Deposit(float fx, float fy, Color c, float intensity) {
		var cx = (int)MathF.Round(fx);
		var cy = (int)MathF.Round(fy);
		if (cx < 0 || cx >= _w || cy < 0 || cy >= _h) {
			return;
		}

		var idx = (cy * _w + cx) * 3;
		_field[idx] = Math.Min(255, _field[idx] + c.R * intensity);
		_field[idx + 1] = Math.Min(255, _field[idx + 1] + c.G * intensity);
		_field[idx + 2] = Math.Min(255, _field[idx + 2] + c.B * intensity);
	}

	private void Compose(Color[] grid) {
		for (var i = 0; i < _w * _h; i++) {
			var j = i * 3;
			grid[i] = Color.FromArgb(
				Math.Min(255, (int)_field[j]),
				Math.Min(255, (int)_field[j + 1]),
				Math.Min(255, (int)_field[j + 2]));
		}
	}

	private sealed class Particle {
		public float X;
		public float Y;
		public float Vx;
		public float Vy;
		public double Hue;
	}
}
