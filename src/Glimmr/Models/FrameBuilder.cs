#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Serilog;

#endregion

namespace Glimmr.Models {
	public class FrameBuilder {
		private const int ScaleHeight = 480;
		private const int ScaleWidth = 640;
		private readonly int _borderHeight = 10;
		private readonly int _borderWidth = 10;

		private readonly int _bottomCount;

		// This will store the coords of input values
		private readonly Rectangle[] _inputCoords;
		private readonly int _ledCount;
		private readonly int _leftCount;
		private readonly int _rightCount;
		private readonly int _topCount;

		public FrameBuilder(int[] inputDimensions, bool sectors = false, bool center = false) {
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
				_inputCoords = DrawGrid();
			}
		}


		public Mat Build(IEnumerable<Color> colors) {
			var enumerable = colors as Color[] ?? colors.ToArray();
			if (enumerable.Length != _ledCount) {
				throw new ArgumentOutOfRangeException(
					$"Color length should be {_ledCount} versus {enumerable.Length}.");
			}

			var gMat = new Mat(new Size(ScaleWidth, ScaleHeight), DepthType.Cv8U, 3);
			for (var i = 0; i < _inputCoords.Length; i++) {
				var col = new Bgr(enumerable.ToArray()[i]).MCvScalar;
				CvInvoke.Rectangle(gMat, _inputCoords[i], col, -1, LineType.AntiAlias);
			}

			return gMat;
		}

		private Rectangle[] DrawGrid() {
			var vOffset = 0;
			var hOffset = 0;
			var output = new Rectangle[_ledCount];

			// Top Region
			var tTop = hOffset;
			// Bottom Region
			var bBottom = ScaleHeight;
			var bTop = bBottom - _borderHeight;

			// Left Column Border
			var lLeft = vOffset;

			// Right Column Border
			var rRight = ScaleWidth;
			var rLeft = rRight - _borderWidth;
			float w = ScaleWidth;
			float h = ScaleHeight;

			// Steps
			var widthTop = (int) Math.Ceiling(w / _topCount);
			var widthBottom = (int) Math.Ceiling(w / _bottomCount);
			var heightLeft = (int) Math.Ceiling(h / _leftCount);
			var heightRight = (int) Math.Ceiling(h / _rightCount);
			// Calc right regions, bottom to top
			var idx = 0;
			var pos = ScaleHeight - heightRight;

			for (var i = 0; i < _rightCount; i++) {
				if (pos < 0) {
					pos = 0;
				}

				output[idx] = new Rectangle(rLeft, pos, _borderWidth, heightRight);
				pos -= heightRight;
				idx++;
			}

			// Calc top regions, from right to left
			pos = ScaleWidth - widthTop;

			for (var i = 0; i < _topCount; i++) {
				if (pos < 0) {
					pos = 0;
				}

				output[idx] = new Rectangle(pos, tTop, widthTop, _borderHeight);
				idx++;
				pos -= widthTop;
			}


			// Calc left regions (top to bottom)
			pos = 0;

			for (var i = 0; i < _leftCount; i++) {
				if (pos > ScaleHeight - heightLeft) {
					pos = ScaleHeight - heightLeft;
				}

				output[idx] = new Rectangle(lLeft, pos, _borderWidth, heightLeft);
				pos += heightLeft;
				idx++;
			}

			// Calc bottom regions (L-R)
			pos = 0;
			for (var i = 0; i < _bottomCount; i++) {
				if (idx >= _ledCount) {
					Log.Warning($"Index is {idx}, but count is {_ledCount}");
					continue;
				}

				if (pos > ScaleWidth - widthBottom) {
					pos = ScaleWidth - widthBottom;
				}

				output[idx] = new Rectangle(pos, bTop, widthBottom, _borderHeight);
				pos += widthBottom;
				idx++;
			}

			if (idx != _ledCount) {
				Log.Warning($"Warning: Led count is {idx - 1}, but should be {_ledCount}");
			}

			return output;
		}

		private Rectangle[] DrawSectors() {
			// This is where we're saving our output
			var fs = new Rectangle[_ledCount];
			// Individual segment sizes
			var sectorWidth = ScaleWidth / _topCount;
			var sectorHeight = ScaleHeight / _leftCount;
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
				var ord = step * sectorHeight;
				var width = sectorHeight * wIdx + 5;
				if (width > ScaleWidth / 2) {
					width = ScaleWidth / 2;
				}

				var right = ScaleWidth - width;
				fs[idx] = new Rectangle(right, ord, ScaleWidth, sectorHeight);
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
				var ord = step * sectorWidth;
				var height = sectorWidth * wIdx + 5;
				if (height > ScaleHeight / 2) {
					height = ScaleHeight / 2;
				}

				fs[idx] = new Rectangle(ord, minTop, sectorWidth, height);
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
				var ord = step * sectorHeight;
				var width = sectorHeight * wIdx + 5;
				if (width > ScaleWidth / 2) {
					width = ScaleWidth / 2;
				}

				fs[idx] = new Rectangle(minLeft, ord, width, sectorHeight);
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
				var ord = step * sectorWidth;
				var height = sectorWidth * wIdx + 5;
				if (height > ScaleHeight / 2) {
					height = ScaleHeight / 2;
				}

				var bottom = ScaleHeight - height;
				fs[idx] = new Rectangle(ord, bottom, sectorWidth, height);
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
}