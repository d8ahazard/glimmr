#region

using System;
using System.Drawing;
using System.Linq;
using Glimmr.Models.ColorSource.Ambient;

#endregion

namespace Glimmr.Models.ColorSource.Scene;

/// <summary>
///     Fills the whole grid with a single solid color. A 1x1 grid is enough - the frame
///     pipeline expands one cell to the entire display.
/// </summary>
public class ColorProvider : ISceneProvider {
	private readonly Color _color;

	public ColorProvider(Color color) {
		_color = color;
	}

	public int Width => 1;
	public int Height => 1;
	public double Fps => 2;

	public void NextFrame(Color[] grid) {
		if (grid.Length > 0) {
			grid[0] = _color;
		}
	}
}

/// <summary>
///     Plays an "image" scene: a 2D color matrix (e.g. Image2Scene output) that scrolls over
///     time. Reuses the proven <see cref="ColorMatrix" /> scroll logic from ambient scenes,
///     shifting one cell every (AnimationTime / MatrixStep) seconds for a smooth crawl.
/// </summary>
public class ImageProvider : ISceneProvider {
	private readonly ColorMatrix _matrix;
	private readonly int _framesPerShift;
	private int _counter;
	private Color[] _current;

	public ImageProvider(SceneV2 scene) {
		var amb = new AmbientScene {
			ColorMatrix = scene.ColorMatrix,
			MatrixDirection = string.IsNullOrEmpty(scene.MatrixDirection) ? "Random" : scene.MatrixDirection,
			MatrixStep = 1 // shift one cell at a time; cadence is controlled below
		};
		_matrix = new ColorMatrix(amb);
		Width = _matrix.Width;
		Height = _matrix.Height;
		Fps = 20;
		var step = Math.Max(1, scene.MatrixStep);
		var anim = scene.AnimationTime <= 0 ? 3f : scene.AnimationTime;
		_framesPerShift = Math.Max(1, (int)Math.Round(anim / step * Fps));
		_current = _matrix.ColorArray().ToArray();
	}

	public int Width { get; }
	public int Height { get; }
	public double Fps { get; }

	public void NextFrame(Color[] grid) {
		if (++_counter >= _framesPerShift) {
			_matrix.Update();
			_current = _matrix.ColorArray().ToArray();
			_counter = 0;
		}

		Array.Copy(_current, grid, Math.Min(grid.Length, _current.Length));
	}
}
