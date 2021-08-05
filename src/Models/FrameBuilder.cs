using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Glimmr.Models.Util;
using Serilog;

namespace Glimmr.Models {
	public class FrameBuilder {
		// This will store the coords of input values
		private readonly Rectangle[] _inputCoords;
		private readonly int _leftCount;
		private readonly int _rightCount;
		private readonly int _topCount;
		private readonly int _bottomCount;
		private readonly int _ledCount;
		private readonly int _scaleHeight = DisplayUtil.CaptureHeight();
		private readonly int _scaleWidth = DisplayUtil.CaptureWidth();
		private readonly int _borderWidth = 10;
		private readonly int _borderHeight = 10;
		
		public FrameBuilder(int[] inputDimensions, bool sectors = false) {
			_leftCount = inputDimensions[0];
			_rightCount = inputDimensions[1];
			_topCount = inputDimensions[2];
			_bottomCount = inputDimensions[3];
			_ledCount = _leftCount + _rightCount + _topCount + _bottomCount;
			if (sectors) {
				_ledCount -= 4;
				_inputCoords = DrawSectors();
			} else {
				_inputCoords = DrawGrid();
			}
		}

		public Mat Build(IEnumerable<Color> colors) {
			var enumerable = colors as Color[] ?? colors.ToArray();
			if (enumerable.Length != _ledCount) {
				throw new ArgumentOutOfRangeException($"Color length should be {_ledCount} versus {enumerable.Length}.");
			}

			var gMat = new Mat(new Size(_scaleWidth,_scaleHeight),DepthType.Cv8U,3);
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
			var bBottom = _scaleHeight;
			var bTop = bBottom - _borderHeight;

			// Left Column Border
			var lLeft = vOffset;

			// Right Column Border
			var rRight = _scaleWidth;
			var rLeft = rRight - _borderWidth;
			float w = _scaleWidth;
			float h = _scaleHeight;

			// Steps
			var widthTop = (int) Math.Ceiling(w / _topCount);
			var widthBottom = (int) Math.Ceiling(w / _bottomCount);
			var heightLeft = (int) Math.Ceiling(h / _leftCount);
			var heightRight = (int) Math.Ceiling(h / _rightCount);
			// Calc right regions, bottom to top
			var idx = 0;
			var pos = _scaleHeight - heightRight;

			for (var i = 0; i < _rightCount; i++) {
				if (pos < 0) {
					pos = 0;
				}

				output[idx] = new Rectangle(rLeft, pos, _borderWidth, heightRight);
				pos -= heightRight;
				idx++;
			}

			// Calc top regions, from right to left
			pos = _scaleWidth - widthTop;

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
				if (pos > _scaleHeight - heightLeft) {
					pos = _scaleHeight - heightLeft;
				}

				output[idx] = new Rectangle(lLeft, pos, _borderWidth, heightLeft);
				pos += heightLeft;
				idx++;
			}

			// Calc bottom regions (L-R)
			pos = 0;
			for (var i = 0; i < _bottomCount; i++) {
				if (idx >= _ledCount) {
					Log.Debug($"Index is {idx}, but count is {_ledCount}");
					continue;
				}

				if (pos > _scaleWidth - widthBottom) {
					pos = _scaleWidth - widthBottom;
				}

				output[idx] = new Rectangle(pos, (int) bTop, widthBottom, (int) _borderHeight);
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
			var sectorWidth = _scaleWidth / _topCount;
			var sectorHeight = _scaleHeight / _leftCount;
			// These are based on the border/strip values
			// Minimum limits for top, bottom, left, right            
			const int minTop = 0;
			var minBot = _scaleHeight - sectorHeight;
			const int minLeft = 0;
			var minRight = _scaleWidth - sectorWidth;
			// Calc right regions, bottom to top
			var idx = 0;
			var step = _rightCount - 1;
			while (step >= 0) {
				var ord = step * sectorHeight;
				fs[idx] = new Rectangle(minRight, ord, sectorWidth, sectorHeight);
				idx++;
				step--;
			}

			// Calc top regions, from right to left, skipping top-right corner (total horizontal sectors minus one)
			step = _topCount - 2;
			while (step > 0) {
				var ord = step * sectorWidth;
				fs[idx] = new Rectangle(ord, minTop, sectorWidth, sectorHeight);
				idx++;
				step--;
			}

			step = 0;
			// Calc left regions (top to bottom), skipping top-left
			while (step <= _leftCount - 1) {
				var ord = step * sectorHeight;
				fs[idx] = new Rectangle(minLeft, ord, sectorWidth, sectorHeight);
				idx++;
				step++;
			}

			step = 1;
			// Calc bottom center regions (L-R)
			while (step <= _bottomCount - 2) {
				var ord = step * sectorWidth;
				fs[idx] = new Rectangle(ord, minBot, sectorWidth, sectorHeight);
				idx++;
				step += 1;
			}

			return fs;
		}
	}
}