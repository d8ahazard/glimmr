#region

using System;
using System.Collections.Generic;
using System.Drawing;

#endregion

namespace Glimmr.Models.ColorSource.Scene.Script;

/// <summary>
///     Executes a <see cref="ParticleSceneSpec" /> live, one frame at a time. A persistent
///     light "field" is decayed each frame (for trails) and particles deposit color additively;
///     the composited result is background + field, clamped. Deliberately simple - this is the
///     fireworks/screensaver tier, not a GPU particle system.
/// </summary>
public class ParticleEngine : ISceneProvider {
	private const int MaxParticles = 6000;
	private const int MaxSpawnDepth = 4;

	private readonly ParticleSceneSpec _spec;
	private readonly Random _rand = new();
	private readonly List<Particle> _particles = new();
	private readonly Dictionary<string, double> _spawnTimers = new(StringComparer.OrdinalIgnoreCase);

	private readonly int _w;
	private readonly int _h;
	private readonly float[] _field; // [w*h*3] accumulated light above the background

	public ParticleEngine(ParticleSceneSpec spec) {
		_spec = spec;
		_w = Math.Max(1, spec.Width);
		_h = Math.Max(1, spec.Height);
		_field = new float[_w * _h * 3];
		foreach (var name in spec.RootEmitters) {
			_spawnTimers[name] = 0;
		}
	}

	public int Width => _w;
	public int Height => _h;
	public double Fps => _spec.Fps;

	public void NextFrame(Color[] grid) {
		var dt = 1.0 / Math.Max(1, _spec.Fps);
		DecayField();
		SpawnFromRoots(dt);
		UpdateParticles(dt);
		Compose(grid);
	}

	private void DecayField() {
		var trail = (float)_spec.Trail;
		for (var i = 0; i < _field.Length; i++) {
			_field[i] *= trail;
		}
	}

	private void SpawnFromRoots(double dt) {
		foreach (var name in _spec.RootEmitters) {
			if (!_spec.Emitters.TryGetValue(name, out var em)) {
				continue;
			}

			var t = _spawnTimers[name] - dt;
			// Guard against pathological 0-interval scripts spinning the spawner.
			var guard = 0;
			while (t <= 0 && guard++ < 64) {
				var n = Math.Max(0, em.Count.SampleInt(_rand));
				for (var i = 0; i < n; i++) {
					Spawn(em, null, 0);
				}

				var interval = em.RateInterval.Sample(_rand);
				if (interval < 0.01) {
					interval = 0.01;
				}

				t += interval;
			}

			_spawnTimers[name] = t;
		}
	}

	private void Spawn(EmitterSpec em, Particle? parent, int depth) {
		if (_particles.Count >= MaxParticles || depth > MaxSpawnDepth) {
			return;
		}

		float px, py;
		if (em.Origin.FromParent && parent != null) {
			px = parent.X;
			py = parent.Y;
		} else {
			px = (float)((em.Origin.XMode == OriginAxis.Random ? _rand.NextDouble() : em.Origin.X) * _w);
			py = (float)((em.Origin.YMode == OriginAxis.Random ? _rand.NextDouble() : em.Origin.Y) * _h);
		}

		var speed = (float)em.Speed.Sample(_rand);
		float vx = 0, vy = 0;
		switch (em.Direction) {
			case SpawnDirection.Up:
				vy = -speed;
				break;
			case SpawnDirection.Down:
				vy = speed;
				break;
			case SpawnDirection.Left:
				vx = -speed;
				break;
			case SpawnDirection.Right:
				vx = speed;
				break;
			case SpawnDirection.Radial:
				var a = _rand.NextDouble() * Math.PI * 2;
				vx = (float)(Math.Cos(a) * speed);
				vy = (float)(Math.Sin(a) * speed);
				break;
			case SpawnDirection.None:
			default:
				break;
		}

		// Optional spray cone: rotate the launch vector by a random angle within +/- Spread/2.
		if (em.Spread > 0 && (vx != 0 || vy != 0)) {
			var spreadRad = em.Spread * Math.PI / 180.0;
			var a = (_rand.NextDouble() - 0.5) * spreadRad;
			var cos = (float)Math.Cos(a);
			var sin = (float)Math.Sin(a);
			var rx = vx * cos - vy * sin;
			var ry = vx * sin + vy * cos;
			vx = rx;
			vy = ry;
		}

		var life = (float)Math.Max(0.05, em.Life.Sample(_rand));
		var p = new Particle {
			X = px,
			Y = py,
			Vx = vx,
			Vy = vy,
			Life = life,
			MaxLife = life,
			LaunchedUp = vy < 0,
			Color = ResolveColor(em, parent),
			Emitter = em,
			Depth = depth
		};
		_particles.Add(p);
	}

	private Color ResolveColor(EmitterSpec em, Particle? parent) {
		switch (em.ColorMode) {
			case ColorMode.Pick:
				return em.Palette.Count > 0 ? em.Palette[_rand.Next(em.Palette.Count)] : Color.White;
			case ColorMode.Random:
				return FromHue(_rand.NextDouble() * 360);
			case ColorMode.Rainbow:
				return FromHue(_rand.NextDouble() * 360);
			case ColorMode.Parent:
				return parent?.Color ?? em.Color;
			case ColorMode.Fixed:
			default:
				return em.Color;
		}
	}

	private void UpdateParticles(double dt) {
		var fdt = (float)dt;
		for (var i = _particles.Count - 1; i >= 0; i--) {
			var p = _particles[i];
			var em = p.Emitter;

			p.Vx += (float)em.GravityX * fdt;
			p.Vy += (float)em.GravityY * fdt;
			p.X += p.Vx * fdt;
			p.Y += p.Vy * fdt;
			p.Life -= fdt;

			if (em.HueShift != 0) {
				p.Color = ShiftHue(p.Color, em.HueShift * dt);
			}

			var apex = em.DieOnApex && p.LaunchedUp && p.Vy >= 0;
			var oob = p.X < -4 || p.X > _w + 4 || p.Y < -4 || p.Y > _h + 4;
			var dead = p.Life <= 0 || apex || oob;

			if (dead) {
				// Out-of-bounds particles drift offscreen; don't fire their burst there.
				if (!oob && em.SpawnOnDeath != null && _rand.NextDouble() <= em.SpawnChance &&
				    _spec.Emitters.TryGetValue(em.SpawnOnDeath, out var child)) {
					var n = Math.Max(0, child.Count.SampleInt(_rand));
					for (var s = 0; s < n; s++) {
						Spawn(child, p, p.Depth + 1);
					}
				}

				_particles.RemoveAt(i);
				continue;
			}

			Deposit(p);
		}
	}

	private void Deposit(Particle p) {
		var cx = (int)MathF.Round(p.X);
		var cy = (int)MathF.Round(p.Y);
		if (cx < 0 || cx >= _w || cy < 0 || cy >= _h) {
			return;
		}

		// Apex-dying particles stay bright; fading particles dim with remaining life.
		var intensity = p.Emitter.DieOnApex ? 1f : Math.Clamp(p.Life / p.MaxLife, 0f, 1f);
		var idx = (cy * _w + cx) * 3;
		_field[idx] += p.Color.R * intensity;
		_field[idx + 1] += p.Color.G * intensity;
		_field[idx + 2] += p.Color.B * intensity;
	}

	private void Compose(Color[] grid) {
		var bg = _spec.Background;
		for (var i = 0; i < _w * _h; i++) {
			var j = i * 3;
			var r = bg.R + (int)_field[j];
			var g = bg.G + (int)_field[j + 1];
			var b = bg.B + (int)_field[j + 2];
			grid[i] = Color.FromArgb(
				r > 255 ? 255 : r,
				g > 255 ? 255 : g,
				b > 255 ? 255 : b);
		}
	}

	private static Color FromHue(double hueDeg) {
		return ShiftHue(Color.FromArgb(255, 0, 0), hueDeg);
	}

	private static Color ShiftHue(Color c, double deltaDeg) {
		// RGB -> HSV -> rotate hue -> RGB. Keeps S/V; only rotates hue.
		double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
		var max = Math.Max(r, Math.Max(g, b));
		var min = Math.Min(r, Math.Min(g, b));
		var v = max;
		var d = max - min;
		var s = max <= 0 ? 0 : d / max;
		double h = 0;
		if (d > 0) {
			if (max == r) {
				h = (g - b) / d % 6;
			} else if (max == g) {
				h = (b - r) / d + 2;
			} else {
				h = (r - g) / d + 4;
			}

			h *= 60;
		}

		h = (h + deltaDeg) % 360;
		if (h < 0) {
			h += 360;
		}

		var cc = v * s;
		var x = cc * (1 - Math.Abs(h / 60 % 2 - 1));
		var m = v - cc;
		double rr = 0, gg = 0, bb = 0;
		switch ((int)(h / 60)) {
			case 0: rr = cc; gg = x; break;
			case 1: rr = x; gg = cc; break;
			case 2: gg = cc; bb = x; break;
			case 3: gg = x; bb = cc; break;
			case 4: rr = x; bb = cc; break;
			default: rr = cc; bb = x; break;
		}

		return Color.FromArgb(
			(int)Math.Round((rr + m) * 255),
			(int)Math.Round((gg + m) * 255),
			(int)Math.Round((bb + m) * 255));
	}

	private sealed class Particle {
		public float X;
		public float Y;
		public float Vx;
		public float Vy;
		public float Life;
		public float MaxLife;
		public bool LaunchedUp;
		public Color Color;
		public EmitterSpec Emitter = null!;
		public int Depth;
	}
}
