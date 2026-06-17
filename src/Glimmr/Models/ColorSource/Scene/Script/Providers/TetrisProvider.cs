#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

#endregion

namespace Glimmr.Models.ColorSource.Scene.Script.Providers;

/// <summary>
///     Self-playing Tetris: tetrominoes drop into random columns, lock into the well, and full
///     rows flash and clear. When the stack tops out the well resets. Purely cosmetic (no input).
///     Params: speed (rows/sec the active piece falls), flash (line-clear flash seconds).
/// </summary>
public sealed class TetrisProvider : ISceneProvider {
	private static readonly (int X, int Y)[][] Shapes = {
		new[] { (0, 0), (1, 0), (2, 0), (3, 0) }, // I
		new[] { (0, 0), (1, 0), (0, 1), (1, 1) }, // O
		new[] { (0, 0), (1, 0), (2, 0), (1, 1) }, // T
		new[] { (1, 0), (2, 0), (0, 1), (1, 1) }, // S
		new[] { (0, 0), (1, 0), (1, 1), (2, 1) }, // Z
		new[] { (0, 0), (0, 1), (1, 1), (2, 1) }, // J
		new[] { (2, 0), (0, 1), (1, 1), (2, 1) } // L
	};

	private static readonly Color[] PieceColors = {
		Color.FromArgb(0, 240, 240), // I cyan
		Color.FromArgb(240, 240, 0), // O yellow
		Color.FromArgb(160, 0, 240), // T purple
		Color.FromArgb(0, 240, 0), // S green
		Color.FromArgb(240, 0, 0), // Z red
		Color.FromArgb(0, 0, 240), // J blue
		Color.FromArgb(240, 160, 0) // L orange
	};

	private readonly Random _rand = new();
	private readonly int _w;
	private readonly int _h;
	private readonly Color[] _well; // locked cells, Black = empty
	private readonly double _fallInterval;
	private readonly double _flashTime;

	private (int X, int Y)[] _piece = Array.Empty<(int, int)>();
	private Color _pieceColor;
	private int _px;
	private int _py;
	private double _fallTimer;
	private List<int> _flashRows = new();
	private double _flashTimer;

	public TetrisProvider(int width, int height, IReadOnlyDictionary<string, double> p) {
		_w = Math.Max(4, width);
		_h = Math.Max(6, height);
		_well = new Color[_w * _h];
		SceneProviderUtil.Clear(_well);
		var speed = Math.Clamp(p.Get("speed", 12), 1, 40);
		_fallInterval = 1.0 / speed;
		_flashTime = Math.Clamp(p.Get("flash", 0.18), 0, 1);
		Spawn();
	}

	public int Width => _w;
	public int Height => _h;
	public double Fps => 30;

	public void NextFrame(Color[] grid) {
		var dt = 1.0 / Fps;

		if (_flashTimer > 0) {
			_flashTimer -= dt;
			Render(grid, true);
			if (_flashTimer <= 0) {
				CollapseRows();
				Spawn();
			}

			return;
		}

		_fallTimer -= dt;
		if (_fallTimer <= 0) {
			_fallTimer += _fallInterval;
			if (Fits(_piece, _px, _py + 1)) {
				_py++;
			} else {
				Lock();
			}
		}

		Render(grid, false);
	}

	private void Spawn() {
		var idx = _rand.Next(Shapes.Length);
		_piece = Rotate(Shapes[idx], _rand.Next(4));
		_pieceColor = PieceColors[idx];
		var maxX = _piece.Max(c => c.X);
		_px = _rand.Next(0, Math.Max(1, _w - maxX));
		_py = -_piece.Min(c => c.Y);
		// Topped out: clear the well for a fresh round.
		if (!Fits(_piece, _px, _py)) {
			SceneProviderUtil.Clear(_well);
		}
	}

	private void Lock() {
		foreach (var (cx, cy) in _piece) {
			var x = _px + cx;
			var y = _py + cy;
			if (x >= 0 && x < _w && y >= 0 && y < _h) {
				_well[y * _w + x] = _pieceColor;
			}
		}

		_flashRows = FullRows();
		if (_flashRows.Count > 0) {
			_flashTimer = _flashTime > 0 ? _flashTime : 0.0001;
		} else {
			Spawn();
		}
	}

	private List<int> FullRows() {
		var rows = new List<int>();
		for (var y = 0; y < _h; y++) {
			var full = true;
			for (var x = 0; x < _w; x++) {
				if (_well[y * _w + x].GetBrightness() <= 0) {
					full = false;
					break;
				}
			}

			if (full) {
				rows.Add(y);
			}
		}

		return rows;
	}

	private void CollapseRows() {
		foreach (var row in _flashRows.OrderBy(r => r)) {
			for (var y = row; y > 0; y--) {
				for (var x = 0; x < _w; x++) {
					_well[y * _w + x] = _well[(y - 1) * _w + x];
				}
			}

			for (var x = 0; x < _w; x++) {
				_well[x] = Color.Black;
			}
		}

		_flashRows = new List<int>();
	}

	private bool Fits((int X, int Y)[] piece, int ox, int oy) {
		foreach (var (cx, cy) in piece) {
			var x = ox + cx;
			var y = oy + cy;
			if (x < 0 || x >= _w || y >= _h) {
				return false;
			}

			if (y >= 0 && _well[y * _w + x].GetBrightness() > 0) {
				return false;
			}
		}

		return true;
	}

	private static (int X, int Y)[] Rotate((int X, int Y)[] piece, int times) {
		var result = piece;
		for (var t = 0; t < times; t++) {
			// (x,y) -> (y,-x), then normalize to non-negative coords.
			var rotated = result.Select(c => (X: c.Y, Y: -c.X)).ToArray();
			var minX = rotated.Min(c => c.X);
			var minY = rotated.Min(c => c.Y);
			result = rotated.Select(c => (c.X - minX, c.Y - minY)).ToArray();
		}

		return result;
	}

	private void Render(Color[] grid, bool flashing) {
		for (var i = 0; i < _well.Length; i++) {
			grid[i] = _well[i];
		}

		if (flashing) {
			foreach (var row in _flashRows) {
				for (var x = 0; x < _w; x++) {
					grid[row * _w + x] = Color.White;
				}
			}

			return;
		}

		foreach (var (cx, cy) in _piece) {
			var x = _px + cx;
			var y = _py + cy;
			if (x >= 0 && x < _w && y >= 0 && y < _h) {
				grid[y * _w + x] = _pieceColor;
			}
		}
	}
}
