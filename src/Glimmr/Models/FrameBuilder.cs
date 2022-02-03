#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Newtonsoft.Json;
using Serilog;
using static Glimmr.Models.GlimmrConstants;

#endregion

namespace Glimmr.Models;

public class FrameBuilder {
	private readonly int _bottomCount;

	// This will store the coords of input values
	private readonly Rectangle[] _inputCoords;
	private readonly int _ledCount;
	private readonly int _leftCount;
	private readonly int _rightCount;
	private readonly int _topCount;

	public FrameBuilder(IReadOnlyList<int> inputDimensions, bool sectors = false, bool center = false) {
		_leftCount = inputDimensions[0];
		_rightCount = inputDimensions[1];
		_topCount = inputDimensions[2];
		_bottomCount = inputDimensions[3];
		_ledCount = _leftCount + _rightCount + _topCount + _bottomCount;
		if (sectors) {
			if (center) {
				_ledCount = _leftCount * _topCount;
				_inputCoords = DrawCenterSectors();
			} else {
				_ledCount -= 4;
				_inputCoords = DrawSectors();
			}
		} else {
			_inputCoords = DrawLeds();
		}
	}


	public Mat? Build(IEnumerable<Color> colors) {
		var enumerable = colors as Color[] ?? colors.ToArray();
		if (enumerable.Length != _ledCount) {
			throw new ArgumentOutOfRangeException(
				$"Color length should be {_ledCount} versus {enumerable.Length}.");
		}

		try {
			var gMat = new Mat(new Size(ScaleWidth, ScaleHeight), DepthType.Cv8U, 3);
			for (var i = 0; i < _inputCoords.Length; i++) {
				var color = enumerable[i];
				var col = new MCvScalar(color.B, color.G, color.R);
				CvInvoke.Rectangle(gMat, _inputCoords[i], col, -1, LineType.AntiAlias);
			}
			return gMat;
		} catch (Exception e) {
			Log.Debug("Exception: " + e.Message + " at " + e.StackTrace + " " + JsonConvert.SerializeObject(e));
		}

		return null;

	}


	private Rectangle[] DrawLeds() {
		// This is where we're saving our output
		var fs = new Rectangle[_ledCount];
		// Individual segment sizes
		var tWidth = (int)Math.Round((float)ScaleWidth / _topCount, MidpointRounding.AwayFromZero);
		var tHeight = ScaleHeight / (_topCount - 5);
		var lHeight = (int)Math.Round((float)ScaleHeight / _leftCount, MidpointRounding.AwayFromZero);
		var lWidth = ScaleWidth / (_leftCount - 5);
		var bWidth = (int)Math.Round((float)ScaleWidth / _bottomCount, MidpointRounding.AwayFromZero);
		var bHeight = ScaleHeight / (_bottomCount - 5);
		var rHeight = (int)Math.Round((float)ScaleHeight / _rightCount, MidpointRounding.AwayFromZero);
		var rWidth = ScaleWidth / (_rightCount - 5);

		// These are based on the border/strip values
		// Minimum limits for top, bottom, left, right            
		const int minTop = 0;
		const int minLeft = 0;
		// Calc right regions, bottom to top
		var idx = 0;
		var step = _rightCount;
		var width = 0;
		var rev = false;
		var max = 0;
		while (step > 0) {
			var ord = step * rHeight;
			var h = ord + rHeight > ScaleWidth ? ScaleWidth - ord : rHeight;
			if (max == 0 && width >= ScaleWidth / 2) {
				max = step;
			}

			if (rev) {
				width -= rWidth;
			} else {
				width += rWidth;
			}

			var right = ScaleWidth - width;
			fs[idx] = new Rectangle(right, ord, width, h);
			if (max != 0 && _rightCount - max == step) {
				rev = true;
			}

			if (width < rHeight) {
				width = rHeight;
			}

			if (width > ScaleWidth / 2) {
				width = ScaleWidth / 2;
			}

			idx++;
			step--;
		}

		// Calc top regions, from right to left, skipping top-right corner (total horizontal sectors minus one)
		step = _topCount;
		var height = 0;
		rev = false;
		max = -1;
		while (step > 0) {
			var ord = step * tWidth;
			if (max == -1 && height >= ScaleHeight / 2) {
				max = step;
			}

			if (rev) {
				height -= tHeight;
			} else {
				height += tHeight;
			}

			var w = ord + tWidth > ScaleWidth ? ScaleWidth - ord : tWidth;
			fs[idx] = new Rectangle(ord, minTop, w, height);

			if (max != -1 && _topCount - max == step) {
				rev = true;
			}

			if (height > ScaleHeight / 2) {
				height = ScaleHeight / 2;
			}

			if (height < tHeight) {
				height = tHeight;
			}

			idx++;
			step--;
		}

		step = 0;
		width = 0;
		rev = false;
		max = -1;
		// Calc left regions (top to bottom), skipping top-left
		while (step < _leftCount) {
			var ord = step * lHeight;
			var h = ord + lHeight > ScaleHeight ? ScaleHeight - ord : lHeight;
			if (max == -1 && width >= ScaleWidth / 2) {
				max = step;
			}

			if (rev) {
				width -= lWidth;
			} else {
				width += lWidth;
			}

			if (step == _leftCount - 1) {
				width = lHeight;
			}

			fs[idx] = new Rectangle(minLeft, ord, width, h);
			if (max != -1 && max == _leftCount - step + 1) {
				rev = true;
			}

			if (width > ScaleWidth / 2) {
				width = ScaleWidth / 2;
			}

			if (width < lHeight) {
				width = lWidth;
			}

			idx++;
			step++;
		}

		step = 0;
		height = 0;
		rev = false;
		max = -1;
		// Calc bottom center regions (L-R)
		while (step < _bottomCount) {
			var ord = step * bWidth;
			if (max == -1 && height >= ScaleHeight / 2) {
				max = step;
			}

			if (rev) {
				height -= bHeight;
			} else {
				height += bHeight;
			}

			var bottom = ScaleHeight - height;
			var w = ord + bWidth > ScaleWidth ? ScaleWidth - ord : bWidth;
			if (step == _bottomCount - 1) {
				height = bWidth;
			}

			fs[idx] = new Rectangle(ord, bottom, w, height);
			if (max != -1 && max == _bottomCount - step - 1) {
				rev = true;
			}

			if (height > ScaleHeight / 2) {
				height = ScaleHeight / 2;
			}

			if (height < bWidth) {
				height = bWidth;
			}

			idx++;
			step += 1;
		}

		return fs;
	}

	private Rectangle[] DrawSectors() {
		// This is where we're saving our output
		var fs = new Rectangle[_ledCount];
		// Individual segment sizes
		var vWidth = ScaleWidth / _topCount;
		var vHeight = ScaleHeight / _topCount;
		var hHeight = ScaleHeight / _leftCount;
		var hWidth = ScaleWidth / _leftCount;
		// These are based on the border/strip values
		// Minimum limits for top, bottom, left, right            
		const int minTop = 0;
		const int minLeft = 0;
		// Calc right regions, bottom to top
		var idx = 0;
		var step = _rightCount - 1;
		var max = _rightCount;
		var wIdx = 1;
		while (step >= 0) {
			var ord = step * hHeight;
			var width = hWidth * wIdx + 5;
			width = step == _rightCount - 1 || step == 0 ? hHeight : width;
			var right = ScaleWidth - width;
			fs[idx] = new Rectangle(right, ord, ScaleWidth, hHeight);
			wIdx += step < max / 2 ? -1 : 1;
			if (wIdx < 1) {
				wIdx = 1;
			}

			if (wIdx > max / 2) {
				wIdx = max / 2;
			}

			idx++;
			step--;
		}

		// Calc top regions, from right to left, skipping top-right corner (total horizontal sectors minus one)
		step = _topCount - 2;
		wIdx = 1;
		max = _topCount;
		while (step > 0) {
			var ord = step * vWidth;
			var height = vHeight * wIdx;
			height = step == _topCount - 2 || step == 1 ? vWidth : height;
			fs[idx] = new Rectangle(ord, minTop, vWidth, height);
			wIdx += step < max / 2 ? -1 : 1;
			if (wIdx < 1) {
				wIdx = 1;
			}

			if (wIdx > max / 2) {
				wIdx = max / 2;
			}

			idx++;
			step--;
		}

		step = 0;
		wIdx = 1;
		max = _leftCount;
		// Calc left regions (top to bottom), skipping top-left
		while (step <= _leftCount - 1) {
			var ord = step * hHeight;
			var width = hWidth * wIdx;
			if (width > ScaleWidth / 2) {
				width = ScaleWidth / 2;
			}

			width = step == 0 || step == _leftCount - 1 ? hHeight : width;
			fs[idx] = new Rectangle(minLeft, ord, width, hHeight);
			wIdx += step < max / 2 ? 1 : -1;
			if (wIdx < 1) {
				wIdx = 1;
			}

			if (wIdx > max / 2) {
				wIdx = max / 2;
			}

			idx++;
			step++;
		}

		step = 1;
		wIdx = 1;
		max = _bottomCount;
		// Calc bottom center regions (L-R)
		while (step <= _bottomCount - 2) {
			var ord = step * vWidth;
			var height = vHeight * wIdx;
			if (height > ScaleHeight / 2) {
				height = ScaleHeight / 2;
			}

			height = step == 1 ? vWidth : height;
			var bottom = ScaleHeight - height;
			fs[idx] = new Rectangle(ord, bottom, vWidth, height);
			wIdx += step < max / 2 ? 1 : -1;
			if (wIdx < 1) {
				wIdx = 1;
			}

			if (wIdx > max / 2) {
				wIdx = max / 2;
			}

			idx++;
			step += 1;
		}

		return fs;
	}

	private Rectangle[] DrawCenterSectors() {
		// This is where we're saving our output
		var fs = new Rectangle[_ledCount];
		// Calculate heights, minus offset for boxing
		// Individual segment sizes
		var sectorWidth = ScaleWidth / _topCount;
		var sectorHeight = ScaleHeight / _leftCount;
		// These are based on the border/strip values
		// Minimum limits for top, bottom, left, right            
		var top = ScaleHeight - sectorHeight;
		var idx = 0;
		for (var v = _leftCount; v > 0; v--) {
			var left = ScaleWidth - sectorWidth;
			for (var h = _topCount; h > 0; h--) {
				fs[idx] = new Rectangle(left, top, sectorWidth, sectorHeight);
				idx++;
				left -= sectorWidth;
			}

			top -= sectorHeight;
		}

		return fs;
	}
}