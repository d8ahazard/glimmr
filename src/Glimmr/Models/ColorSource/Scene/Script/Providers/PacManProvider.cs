#region

using System;
using System.Collections.Generic;
using System.Drawing;

#endregion

namespace Glimmr.Models.ColorSource.Scene.Script.Providers;

/// <summary>
///     Pac-Man sweeps the grid row by row (boustrophedon), chomping a field of dim dots while a
///     ghost chases a few cells behind. When the board is cleared the dots refill and a new chase
///     begins. Cosmetic only. Params: speed (cells/sec).
/// </summary>
public sealed class PacManProvider : ISceneProvider {
	private static readonly Color[] GhostColors = {
		Color.FromArgb(255, 60, 60), // Blinky
		Color.FromArgb(255, 160, 220), // Pinky
		Color.FromArgb(60, 230, 230), // Inky
		Color.FromArgb(255, 170, 60) // Clyde
	};

	private readonly Random _rand = new();
	private readonly int _w;
	private readonly int _h;
	private readonly bool[] _dots;
	private readonly double _speed;

	private double _x;
	private int _row;
	private int _dir = 1; // +1 = moving right, -1 = moving left
	private double _chomp;
	private Color _ghostColor;
	private readonly Queue<(double X, int Row)> _trail = new();

	public PacManProvider(int width, int height, IReadOnlyDictionary<string, double> p) {
		_w = Math.Max(4, width);
		_h = Math.Max(2, height);
		_dots = new bool[_w * _h];
		_speed = Math.Clamp(p.Get("speed", 14), 2, 40);
		_ghostColor = GhostColors[_rand.Next(GhostColors.Length)];
		Seed();
	}

	public int Width => _w;
	public int Height => _h;
	public double Fps => 30;

	private void Seed() {
		for (var i = 0; i < _dots.Length; i++) {
			_dots[i] = true;
		}

		_row = 0;
		_dir = 1;
		_x = 0;
		_trail.Clear();
		_ghostColor = GhostColors[_rand.Next(GhostColors.Length)];
	}

	public void NextFrame(Color[] grid) {
		var dt = 1.0 / Fps;
		_chomp += dt * 8;
		_x += _dir * _speed * dt;

		// Eat the dot at the current cell.
		var cx = (int)Math.Round(_x);
		if (cx >= 0 && cx < _w) {
			_dots[_row * _w + cx] = false;
		}

		_trail.Enqueue((_x, _row));
		while (_trail.Count > 6) {
			_trail.Dequeue();
		}

		// End of a row: drop to the next and reverse direction (lawnmower path).
		if ((_dir > 0 && _x >= _w - 1) || (_dir < 0 && _x <= 0)) {
			_row++;
			_dir = -_dir;
			_x = Math.Clamp(_x, 0, _w - 1);
			if (_row >= _h) {
				Seed();
			}
		}

		Render(grid);
	}

	private void Render(Color[] grid) {
		// Dim dots remaining to be eaten.
		for (var i = 0; i < _dots.Length; i++) {
			grid[i] = _dots[i] ? Color.FromArgb(30, 30, 70) : Color.Black;
		}

		// Ghost a few cells back along the trail.
		if (_trail.Count > 0) {
			var ghost = _trail.Peek();
			Plot(grid, (int)Math.Round(ghost.X), ghost.Row, _ghostColor);
		}

		// Pac-Man: brightness pulse stands in for the chomp on a low-res grid.
		var open = (Math.Sin(_chomp) + 1) / 2; // 0..1
		var pac = SceneProviderUtil.Scale(Color.FromArgb(255, 240, 0), 0.55 + 0.45 * open);
		Plot(grid, (int)Math.Round(_x), _row, pac);
	}

	private void Plot(Color[] grid, int x, int y, Color c) {
		if (x >= 0 && x < _w && y >= 0 && y < _h) {
			grid[y * _w + x] = c;
		}
	}
}
