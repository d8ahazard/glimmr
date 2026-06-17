#region

using System;
using System.Collections.Generic;
using System.Drawing;

#endregion

namespace Glimmr.Models.ColorSource.Scene.Script.Providers;

/// <summary>
///     Conway's Game of Life. Live cells are colored by age (young = warm, old = cool) and fade
///     in/out for a soft look. Re-seeds when the population stalls or dies out. Params: speed
///     (generations/sec), density (initial fill 0..1), hue (age color start, degrees).
/// </summary>
public sealed class GameOfLifeProvider : ISceneProvider {
	private readonly Random _rand = new();
	private readonly double _density;
	private readonly double _hue;
	private readonly int _w;
	private readonly int _h;

	private bool[] _cells;
	private bool[] _next;
	private int[] _age;
	private readonly float[] _bright; // smoothed per-cell brightness for soft transitions

	private readonly double _genInterval;
	private double _genTimer;
	private int _staleGenerations;
	private int _lastPopulation = -1;

	public GameOfLifeProvider(int width, int height, IReadOnlyDictionary<string, double> p) {
		_w = Math.Max(2, width);
		_h = Math.Max(2, height);
		_density = Math.Clamp(p.Get("density", 0.32), 0.05, 0.9);
		_hue = p.Get("hue", 30);
		var speed = Math.Clamp(p.Get("speed", 8), 0.5, 30);
		_genInterval = 1.0 / speed;
		_cells = new bool[_w * _h];
		_next = new bool[_w * _h];
		_age = new int[_w * _h];
		_bright = new float[_w * _h];
		Seed();
	}

	public int Width => _w;
	public int Height => _h;
	public double Fps => 30;

	private void Seed() {
		for (var i = 0; i < _cells.Length; i++) {
			_cells[i] = _rand.NextDouble() < _density;
			_age[i] = _cells[i] ? 1 : 0;
		}

		_staleGenerations = 0;
		_lastPopulation = -1;
	}

	public void NextFrame(Color[] grid) {
		_genTimer -= 1.0 / Fps;
		if (_genTimer <= 0) {
			Step();
			_genTimer += _genInterval;
		}

		// Render with eased brightness so cells don't pop harshly between generations.
		for (var i = 0; i < _cells.Length; i++) {
			var target = _cells[i] ? 1f : 0f;
			_bright[i] += (target - _bright[i]) * 0.35f;
			if (_bright[i] < 0.02f) {
				grid[i] = Color.Black;
				continue;
			}

			// Hue drifts with age: fresh cells near the base hue, survivors rotate toward cool.
			var hue = _hue + Math.Min(_age[i], 40) * 4;
			grid[i] = SceneProviderUtil.Scale(SceneProviderUtil.Hsv(hue, 0.8, 1.0), _bright[i]);
		}
	}

	private void Step() {
		var pop = 0;
		for (var y = 0; y < _h; y++) {
			for (var x = 0; x < _w; x++) {
				var idx = y * _w + x;
				var n = CountNeighbors(x, y);
				var alive = _cells[idx];
				var nextAlive = alive ? n == 2 || n == 3 : n == 3;
				_next[idx] = nextAlive;
				if (nextAlive) {
					pop++;
					_age[idx] = alive ? Math.Min(_age[idx] + 1, 999) : 1;
				} else {
					_age[idx] = 0;
				}
			}
		}

		(_cells, _next) = (_next, _cells);

		// Detect a dead or static board and re-seed so the scene never freezes.
		if (pop == 0 || pop == _lastPopulation) {
			_staleGenerations++;
		} else {
			_staleGenerations = 0;
		}

		_lastPopulation = pop;
		if (pop == 0 || _staleGenerations > 18) {
			Seed();
		}
	}

	private int CountNeighbors(int x, int y) {
		var count = 0;
		for (var dy = -1; dy <= 1; dy++) {
			for (var dx = -1; dx <= 1; dx++) {
				if (dx == 0 && dy == 0) {
					continue;
				}

				// Toroidal wrap so gliders cruise off one edge and back on the other.
				var nx = (x + dx + _w) % _w;
				var ny = (y + dy + _h) % _h;
				if (_cells[ny * _w + nx]) {
					count++;
				}
			}
		}

		return count;
	}
}
