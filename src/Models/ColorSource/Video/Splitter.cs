#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.Video {
	public class Splitter {
		private const int ScaleHeight = DisplayUtil.CaptureHeight;
		private const int ScaleWidth = DisplayUtil.CaptureWidth;
		private readonly float _borderHeight;

		// The width of the border to crop from for LEDs
		private readonly float _borderWidth;

		private readonly ControlService _controlService;
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
		private bool _useCenter;
		private readonly Stopwatch _frameWatch;

		private Rectangle[] _fullCoords;
		private Rectangle[] _fullSectors;
		private bool _hCrop;
		private int _hCropCheck;
		private int _hCropCount;
		private int _hCropPixels;
		private int _hSectors;
		private Mat _input;
		private int _leftCount;

		private int _ledCount;
		private int _sectorCount;

		private int _previewMode;
		private int _rightCount;


		// Set this when the sector changes
		private bool _sectorChanged;
		private int _topCount;

		// Are we cropping right now?
		private bool _vCrop;

		// Where we save the potential new value between checks
		private int _vCropCheck;
		private int _vCropCount;

		// Current crop settings
		private int _vCropPixels;
		private int _vSectors;


		public Splitter(ControlService controlService) {
			_frameWatch = new Stopwatch();
			_frameWatch.Start();
			_controlService = controlService;
			_controlService.RefreshSystemEvent += RefreshSystem;
			RefreshSystem();
			// Set desired width of capture region to 15% total image
			_borderWidth = 10;
			_borderHeight = 10;
			Log.Debug($"LED, sector counts: {_ledCount}, {_sectorCount}");
			// Get sectors
			_fullCoords = DrawGrid();
			_fullSectors = DrawSectors();
			Log.Debug("Splitter init complete.");
		}

		
		private void RefreshSystem() {
			var sd = DataUtil.GetSystemData();
			_leftCount = sd.LeftCount;
			_topCount = sd.TopCount;
			_rightCount = sd.RightCount == 0 ? _leftCount : sd.RightCount;
			_bottomCount = sd.BottomCount == 0 ? _topCount : sd.BottomCount;
			_hSectors = sd.HSectors;
			_vSectors = sd.VSectors;
			_cropDelay = sd.CropDelay;
			_cropLetter = sd.EnableLetterBox;
			_cropPillar = sd.EnablePillarBox;
			_useCenter = sd.UseCenter;
			_ledCount = sd.LedCount;
			_sectorCount = sd.SectorCount;
			if (_ledCount == 0) _ledCount = 200;
			if (_sectorCount == 0) _sectorCount = 12;

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
			_input = inputMat ?? throw new ArgumentException("Invalid input material.");
			// Don't do anything if there's no frame.
			if (_input.IsEmpty) {
				Log.Debug("SPLITTER: NO INPUT.");
				return;
			}

			// Check sectors once per second
			if (_frameWatch.Elapsed >= TimeSpan.FromSeconds(1)) {
				CheckSectors();
				_frameWatch.Restart();
			}

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
			inputMat.CopyTo(gMat);
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

			// Bottom Region
			var bBottom = ScaleHeight - hOffset;
			var bTop = bBottom - _borderHeight;

			// Left Column Border
			var lLeft = vOffset;

			// Right Column Border
			var rRight = ScaleWidth - vOffset;
			var rLeft = rRight - _borderWidth;

			// Steps
			var widthTop = (float) ScaleWidth / _topCount;
			var widthBottom = (float) ScaleWidth / _bottomCount;
			var heightLeft = (float) ScaleHeight / _leftCount;
			var heightRight = (float) ScaleHeight / _rightCount;

			// Calc right regions, bottom to top
			var pos = ScaleHeight - heightRight;
			var idx = 0;
			while (pos >= 0) {
				output[idx] = new Rectangle((int) rLeft, (int) pos, (int) _borderWidth, (int) heightRight);
				pos -= heightRight;
				idx++;
			}

			if (pos > -.002) {
				output[idx] = new Rectangle((int) rLeft, 0, (int) _borderWidth, (int) heightRight);
				idx++;
			}

			// Calc top regions, from right to left
			pos = ScaleWidth - widthTop;
			while (pos >= 0) {
				output[idx] = new Rectangle((int) pos, tTop, (int) widthTop, (int) _borderHeight);
				idx++;
				pos -= widthTop;
			}

			if (pos > -.002) {
				output[idx] = new Rectangle(0, tTop, (int) widthTop, (int) _borderHeight);
				idx++;
			}

			// Calc left regions (top to bottom)
			pos = 0;
			while (pos < ScaleHeight) {
				output[idx] = new Rectangle(lLeft, (int) pos, (int) _borderWidth, (int) heightLeft);
				pos += heightLeft;
				idx++;
			}

			// Calc bottom regions (L-R)
			pos = 0;
			while (pos < ScaleWidth && idx < _ledCount) {
				output[idx] = new Rectangle((int) pos, (int) bTop, (int) widthBottom, (int) _borderHeight);
				pos += widthBottom;
				idx++;
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
			var sectorWidth = (ScaleWidth - hOffset * 2) / _hSectors;
			var sectorHeight = (ScaleHeight - vOffset * 2) / _vSectors;
			// These are based on the border/strip values
			// Minimum limits for top, bottom, left, right            
			var top = ScaleHeight - vOffset - sectorHeight;
			var idx = 0;
			for (var v = _vSectors; v > 0; v--) {
				var left = ScaleWidth - hOffset - sectorWidth;
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
			// Individual segment sizes
			var sectorWidth = (ScaleWidth - hOffset * 2) / _hSectors;
			var sectorHeight = (ScaleHeight - vOffset * 2) / _vSectors;
			// These are based on the border/strip values
			// Minimum limits for top, bottom, left, right            
			var minTop = vOffset;
			var minBot = ScaleHeight - vOffset - sectorHeight;
			var minLeft = hOffset;
			var minRight = ScaleWidth - hOffset - sectorWidth;
			// Calc right regions, bottom to top
			var idx = 0;
			var step = _vSectors - 1;
			while (step >= 0) {
				var ord = step * sectorHeight + vOffset;
				fs[idx] = new Rectangle(minRight, ord, sectorWidth, sectorHeight);
				idx++;
				step--;
			}

			// Calc top regions, from right to left, skipping top-right corner (total horizontal sectors minus one)
			step = _hSectors - 2;
			while (step >= 0) {
				var ord = step * sectorWidth + hOffset;
				fs[idx] = new Rectangle(ord, minTop, sectorWidth, sectorHeight);
				idx++;
				step--;
			}

			step = 1;
			// Calc left regions (top to bottom), skipping top-left
			while (step <= _vSectors - 1) {
				var ord = step * sectorHeight + vOffset;
				fs[idx] = new Rectangle(minLeft, ord, sectorWidth, sectorHeight);
				idx++;
				step++;
			}

			step = 1;
			// Calc bottom center regions (L-R)
			while (step <= _hSectors - 2) {
				var ord = step * sectorWidth + hOffset;
				fs[idx] = new Rectangle(ord, minBot, sectorWidth, sectorHeight);
				idx++;
				step += 1;
			}

			return fs;
		}

		public void Refresh() {
			var sd = DataUtil.GetSystemData();
			_previewMode = sd.PreviewMode;
			Log.Debug("Preview mode set to: " + _previewMode);
			_leftCount = sd.LeftCount;
			_topCount = sd.TopCount;
			_rightCount = sd.RightCount;
			_bottomCount = sd.BottomCount;
			_hSectors = sd.HSectors;
			_vSectors = sd.VSectors;
			_ledCount = sd.LedCount;
			_sectorCount = sd.SectorCount;
			if (_hSectors == 0) {
				_hSectors = 10;
			}

			if (_vSectors == 0) {
				_vSectors = 6;
			}
			
			_sectorChanged = true;
			_fullCoords = new Rectangle[_ledCount];
			_fullSectors = new Rectangle[_sectorCount];
			_fullCoords = DrawGrid();
			_fullSectors = DrawSectors();
			
		}
	}
}