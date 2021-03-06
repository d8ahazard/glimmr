﻿#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.Video {
	public class Splitter {
		private int _scaleHeight = DisplayUtil.CaptureHeight();
		private int _scaleWidth = DisplayUtil.CaptureWidth();
		private readonly float _borderHeight;

		// The width of the border to crop from for LEDs
		private readonly float _borderWidth;

		private readonly ControlService _controlService;
		private readonly Stopwatch _frameWatch;
		public bool DoSave;
		public bool NoImage;
		private int _bottomCount;

		// The current crop mode?
		private Color[] _colorsLed;
		private Color[] _colorsSectors;
		private int _cropDelay;

		// Loaded settings
		private bool _cropLetter;
		private bool _cropPillar;

		private Rectangle[] _fullCoords;
		private Rectangle[] _fullSectors;
		private bool _hCrop;
		private int _hCropCheck;
		private int _hCropCount;
		private int _hCropPixels;
		private int _hSectors;
		private Mat? _input;

		private int _ledCount;
		private int _leftCount;

		private int _previewMode;
		private int _rightCount;


		// Set this when the sector changes
		private bool _sectorChanged;
		private int _sectorCount;
		private int _topCount;
		private bool _useCenter;

		// Are we cropping right now?
		private bool _vCrop;

		// Where we save the potential new value between checks
		private int _vCropCheck;
		private int _vCropCount;

		// Current crop settings
		private int _vCropPixels;
		private int _vSectors;


		public Splitter(ControlService controlService) {
			_colorsLed = Array.Empty<Color>();
			_colorsSectors = Array.Empty<Color>();
			_frameWatch = new Stopwatch();
			_frameWatch.Start();
			_controlService = controlService;
			_controlService.RefreshSystemEvent += RefreshSystem;
			RefreshSystem();
			// Set desired width of capture region to 15% total image
			_borderWidth = 10;
			_borderHeight = 10;
			// Get sectors
			_fullCoords = DrawGrid();
			_fullSectors = DrawSectors();
		}


		private void RefreshSystem() {
			var sd = DataUtil.GetSystemData();
			_leftCount = sd.LeftCount;
			_topCount = sd.TopCount;
			_rightCount = sd.RightCount;
			_bottomCount = sd.BottomCount;
			_hSectors = sd.HSectors;
			_vSectors = sd.VSectors;
			_cropDelay = sd.CropDelay;
			_cropLetter = sd.EnableLetterBox;
			_cropPillar = sd.EnablePillarBox;
			if (!_cropLetter) {
				_vCrop = false;
				_vCropCheck = 0;
				_vCropPixels = 0;
				_vCropCount = 0;
			}
			if (!_cropPillar) {
				_hCrop = false;
				_hCropCheck = 0;
				_hCropPixels = 0;
				_hCropCount = 0;
			}
			_useCenter = sd.UseCenter;
			_ledCount = sd.LedCount;
			_sectorCount = sd.SectorCount;
			_scaleHeight = DisplayUtil.CaptureHeight();
			_scaleWidth = DisplayUtil.CaptureWidth();
			_sectorChanged = true;
			if (_ledCount == 0) {
				_ledCount = 200;
			}

			if (_sectorCount == 0) {
				_sectorCount = 12;
			}

			_previewMode = sd.PreviewMode;

			// Start our stopwatches for cropping if they were previously disabled
			if (_cropLetter || _cropPillar && !_frameWatch.IsRunning) {
				_frameWatch.Restart();
			}

			// If not cropping, then we don't need a stopwatch
			if (!_cropLetter && !_cropPillar) {
				_frameWatch.Stop();
			}
		}


		public void Update(Mat inputMat) {
			
			var frame = inputMat ?? throw new ArgumentException("Invalid input material.");
			var img = frame.ToImage<Bgr, byte>();
			var sized = img.Resize(_scaleWidth, _scaleHeight,Inter.Cubic);
			_input = sized.Mat;
			
			// Don't do anything if there's no frame.
			if (_input == null || _input.IsEmpty) {
				return;
			}

			// Check sectors once per second
			if (_frameWatch.Elapsed >= TimeSpan.FromSeconds(1)) {
				CheckSectors();
				_frameWatch.Restart();
			}

			//Log.Debug("Input dims are " + _input.Width + " and " + _input.Height);
			// Only calculate new sectors if the value has changed
			if (_sectorChanged) {
				_sectorChanged = false;
				_fullCoords = DrawGrid();
				_fullSectors = DrawSectors();
			}

			var ledColors = new Color[_ledCount];
			for (var i = 0; i < _fullCoords.Length; i++) {
				var sub = new Mat(_input, _fullCoords[i]);
				ledColors[i] = GetAverage(sub);
				sub.Dispose();
			}

			var sectorColors = new Color[_sectorCount];
			for (var i = 0; i < _fullSectors.Length; i++) {
				var sub = new Mat(_input, _fullSectors[i]);
				sectorColors[i] = GetAverage(sub);
				sub.Dispose();
			}

			_colorsLed = ledColors;
			_colorsSectors = sectorColors;

			// Save a preview image if desired
			if (!DoSave) {
				return;
			}

			DoSave = false;

			var path = Directory.GetCurrentDirectory();
			var fullPath = Path.Join(path, "wwwroot", "img", "_preview_output.jpg");
			var gMat = new Mat();
			_input.CopyTo(gMat);
			var colBlack = new Bgr(Color.FromArgb(0, 0, 0, 0)).MCvScalar;
			if (_previewMode == 1) {
				for (var i = 0; i < _fullCoords.Length; i++) {
					var col = new Bgr(_colorsLed[i]).MCvScalar;
					CvInvoke.Rectangle(gMat, _fullCoords[i], col, -1, LineType.AntiAlias);
					CvInvoke.Rectangle(gMat, _fullCoords[i], colBlack, 1, LineType.AntiAlias);
				}
			}

			if (_previewMode == 2) {
				for (var i = 0; i < _fullSectors.Length; i++) {
					var s = _fullSectors[i];
					var col = new Bgr(_colorsSectors[i]).MCvScalar;
					CvInvoke.Rectangle(gMat, s, col, -1, LineType.AntiAlias);
					CvInvoke.Rectangle(gMat, s, colBlack, 1, LineType.AntiAlias);
					var cInt = i + 1;
					var tPoint = new Point(s.X, s.Y + 30);
					CvInvoke.PutText(gMat, cInt.ToString(), tPoint, FontFace.HersheySimplex, 1.0, colBlack);
				}
			}

			gMat.Save(fullPath);
			gMat.Dispose();
			_controlService.TriggerImageUpdate();
		}


		private static Color GetAverage(Mat sInput) {
			var output = sInput.ToImage<Bgr, byte>();
			var avg = output.GetAverage();
			if (avg.Red < 6 && avg.Green < 6 && avg.Blue < 6) {
				return Color.FromArgb(0, 0, 0, 0);
			}

			return Color.FromArgb(255, (int) avg.Red, (int) avg.Green, (int) avg.Blue);
		}

		public List<Color> GetColors() {
			return _colorsLed.ToList();
		}

		public List<Color> GetSectors() {
			return _colorsSectors.ToList();
		}

		private void CheckSectors() {
			if (_input == null) return;
			// If nothing to crop, just leave
			if (!_cropLetter && !_cropPillar) {
				return;
			}

			// Number of pixels to check per loop. Smaller = more accurate, larger = faster?
			const int checkSize = 1;
			const int blackLevel = 2;
			// Set our starting values to zero per loop
			var cropHorizontal = 0;
			var cropVertical = 0;

			var gr = new Mat();
			CvInvoke.CvtColor(_input, gr, ColorConversion.Bgr2Gray);
			// Check to see if everything is black
			var allB = CvInvoke.CountNonZero(gr);
			NoImage = allB == 0;
			// If it is, we can stop here
			if (NoImage) {
				gr.Dispose();
				return;
			}

			if (_cropLetter) {
				for (var r = 0; r <= _input.Height / 4; r += checkSize) {
					// Define top position of bottom section
					var r2 = _input.Height - r - checkSize;
					// Regions to check
					var s1 = new Rectangle(0, r, _input.Width, checkSize);
					var s2 = new Rectangle(0, r2, _input.Width, checkSize);
					var t1 = new Mat(gr, s1);
					var t2 = new Mat(gr, s2);
					var l1 = GetBrightestColor(t1);
					var l2 = GetBrightestColor(t2);
					t1.Dispose();
					t2.Dispose();
					if (!IsBlack(l1, l2, blackLevel)) {
						break;
					}

					cropHorizontal = r;
				}
			}


			if (_cropPillar) {
				for (var c = 0; c < _input.Width / 4; c += checkSize) {
					// Define left coord of right sector
					var c2 = _input.Width - c - checkSize;
					// Create rect for left side check, make it a Mat
					var s1 = new Rectangle(c, 0, checkSize, _input.Height);
					var s2 = new Rectangle(c2, 0, 1, _input.Height);
					var t1 = new Mat(gr, s1);
					var t2 = new Mat(gr, s2);
					var n1 = GetBrightestColor(t1);
					var n2 = GetBrightestColor(t2);
					t1.Dispose();
					t2.Dispose();
					// If it is also black, set that to our current check value
					if (!IsBlack(n1, n2, blackLevel)) {
						break;
					}

					cropVertical = c;
				}
			}


			gr.Dispose();

			if (_cropLetter && cropHorizontal != 0) {
				if (Math.Abs(cropHorizontal - _hCropCheck) <= 4) {
					_hCropCount++;
				} else {
					_hCropCount--;
				}

				if (_hCropCount <= 0) {
					_hCropCount = 0;
				}

				if (_hCropCount >= _cropDelay) {
					_hCropCount = _cropDelay;
				}
			} else {
				_hCropCount = 0;
			}

			if (_hCropCount >= _cropDelay && !_hCrop) {
				_sectorChanged = true;
				_hCrop = true;
				_hCropPixels = cropHorizontal;
			} else if (_hCrop && _hCropCount == 0) {
				_hCrop = false;
				_hCropPixels = 0;
				_sectorChanged = true;
			}

			_hCropCheck = cropHorizontal;

			if (_cropPillar && cropVertical != 0) {
				if (Math.Abs(cropVertical - _vCropCheck) <= 4) {
					_vCropCount++;
				} else {
					_vCropCount--;
				}

				if (_vCropCount <= 0) {
					_vCropCount = 0;
				}

				if (_vCropCount >= _cropDelay) {
					_vCropCount = _cropDelay;
				}
			} else {
				_vCropCount = 0;
			}

			if (_vCropCount >= _cropDelay && !_vCrop) {
				_sectorChanged = true;
				_vCrop = true;
				_vCropPixels = cropVertical;
			} else if (_vCrop && _vCropCount == 0) {
				_vCrop = false;
				_vCropPixels = 0;
				_sectorChanged = true;
			}

			_vCropCheck = cropVertical;
		}

		private Color GetBrightestColor(Mat input) {
			var image = input.ToImage<Bgr, byte>();
			var maxR = 0;
			var maxG = 0;
			var maxB = 0;
			for (var r = 0; r < image.Rows; r++) {
				for (var c = 0; c < image.Cols; c++) {
					var col = image[r, c];
					maxR = Math.Max((int) col.Red, maxR);
					maxG = Math.Max((int) col.Green, maxG);
					maxB = Math.Max((int) col.Blue, maxB);
				}
			}

			return Color.FromArgb(maxR, maxG, maxB);
		}

		private static bool IsBlack(Color input, Color input2, int blackLevel) {
			return input.R <= blackLevel && input.B <= blackLevel && input.G <= blackLevel && input2.R <= blackLevel &&
			       input2.B <= blackLevel && input2.G <= blackLevel;
		}

		private Rectangle[] DrawGrid() {
			var vOffset = _vCropPixels;
			var hOffset = _hCropPixels;
			var output = new Rectangle[_ledCount];

			// Top Region
			var tTop = hOffset;
			Log.Debug("Height and width should be " + _scaleHeight + " and " + _scaleWidth);
			// Bottom Region
			var bBottom = _scaleHeight - hOffset;
			var bTop = bBottom - _borderHeight;

			// Left Column Border
			var lLeft = vOffset;

			// Right Column Border
			var rRight = _scaleWidth - vOffset;
			var rLeft = rRight - _borderWidth;
			float w = _scaleWidth;
			float h = _scaleHeight;

			// Steps
			var widthTop = (int) Math.Ceiling(w / _topCount);
			var widthBottom = (int) Math.Ceiling(w / _bottomCount);
			var heightLeft = (int) Math.Ceiling(h / _leftCount);
			var heightRight = (int) Math.Ceiling(h / _rightCount);
			Log.Debug($"Pxsizes: {widthTop}, {widthBottom}, {heightLeft}, {heightRight}");
			// Calc right regions, bottom to top
			var idx = 0;
			var pos = _scaleHeight - heightRight;

			for (var i = 0; i < _rightCount; i++) {
				if (pos < 0) {
					pos = 0;
				}

				output[idx] = new Rectangle((int) rLeft, pos, (int) _borderWidth, heightRight);
				pos -= heightRight;
				idx++;
			}

			// Calc top regions, from right to left
			pos = _scaleWidth - widthTop;

			for (var i = 0; i < _topCount; i++) {
				if (pos < 0) {
					pos = 0;
				}

				output[idx] = new Rectangle(pos, tTop, widthTop, (int) _borderHeight);
				idx++;
				pos -= widthTop;
			}


			// Calc left regions (top to bottom)
			pos = 0;

			for (var i = 0; i < _leftCount; i++) {
				if (pos > _scaleHeight - heightLeft) {
					pos = _scaleHeight - heightLeft;
				}

				output[idx] = new Rectangle(lLeft, pos, (int) _borderWidth, heightLeft);
				pos += heightLeft;
				idx++;
			}

			// Calc bottom regions (L-R)
			pos = 0;
			Log.Debug($"Calculating bottom, {bTop} {widthBottom} {pos} {_borderHeight} {_bottomCount}");
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


		private Rectangle[] DrawCenterSectors() {
			var hOffset = _hCropPixels;
			var vOffset = _vCropPixels;
			if (_hSectors == 0) {
				_hSectors = 10;
			}

			if (_vSectors == 0) {
				_vSectors = 6;
			}

			// This is where we're saving our output
			var fs = new Rectangle[_sectorCount];
			// Calculate heights, minus offset for boxing
			// Individual segment sizes
			var sectorWidth = (_scaleWidth - hOffset * 2) / _hSectors;
			var sectorHeight = (_scaleHeight - vOffset * 2) / _vSectors;
			// These are based on the border/strip values
			// Minimum limits for top, bottom, left, right            
			var top = _scaleHeight - vOffset - sectorHeight;
			var idx = 0;
			for (var v = _vSectors; v > 0; v--) {
				var left = _scaleWidth - hOffset - sectorWidth;
				for (var h = _hSectors; h > 0; h--) {
					fs[idx] = new Rectangle(left, top, sectorWidth, sectorHeight);
					idx++;
					left -= sectorWidth;
				}

				top -= sectorHeight;
			}

			return fs;
		}

		private Rectangle[] DrawSectors() {
			if (_useCenter) {
				return DrawCenterSectors();
			}

			// How many sectors does each region have?
			var hOffset = _vCropPixels;
			var vOffset = _hCropPixels;
			if (_hSectors == 0) {
				_hSectors = 10;
			}

			if (_vSectors == 0) {
				_vSectors = 6;
			}

			// This is where we're saving our output
			var fs = new Rectangle[_sectorCount];
			// Individual segment sizes
			const int squareSize = 40;
			var sectorWidth = (_scaleWidth - hOffset * 2) / _hSectors;
			var sectorHeight = (_scaleHeight - vOffset * 2) / _vSectors;
			// These are based on the border/strip values
			// Minimum limits for top, bottom, left, right            
			var minTop = vOffset;
			var minBot = _scaleHeight - vOffset - squareSize;
			var minLeft = hOffset;
			var minRight = _scaleWidth - hOffset - squareSize;
			// Calc right regions, bottom to top
			var idx = 0;
			var step = _vSectors - 1;
			while (step >= 0) {
				var ord = step * sectorHeight + vOffset;
				fs[idx] = new Rectangle(minRight, ord, squareSize, sectorHeight);
				idx++;
				step--;
			}

			// Calc top regions, from right to left, skipping top-right corner (total horizontal sectors minus one)
			step = _hSectors - 2;
			while (step > 0) {
				var ord = step * sectorWidth + hOffset;
				fs[idx] = new Rectangle(ord, minTop, sectorWidth, squareSize);
				idx++;
				step--;
			}

			step = 0;
			// Calc left regions (top to bottom), skipping top-left
			while (step <= _vSectors - 1) {
				var ord = step * sectorHeight + vOffset;
				fs[idx] = new Rectangle(minLeft, ord, squareSize, sectorHeight);
				idx++;
				step++;
			}

			step = 1;
			// Calc bottom center regions (L-R)
			while (step <= _hSectors - 2) {
				var ord = step * sectorWidth + hOffset;
				fs[idx] = new Rectangle(ord, minBot, sectorWidth, squareSize);
				idx++;
				step += 1;
			}

			return fs;
		}

		public void Refresh() {
			RefreshSystem();
		}
	}
}