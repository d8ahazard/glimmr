#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.Video {
	public class Splitter {
		private Mat _input;
		private int _leftCount;
		private int _topCount;
		private int _rightCount;
		private int _bottomCount;
		private int _vSectors;
		private int _hSectors;
		
		private List<Rectangle> _fullCoords;
		private List<Rectangle> _fullSectors;
		
		// Current crop settings
		private int _vCropPixels;
		private int _hCropPixels;

		// Where we save the potential new value between checks
		private int _vCropCheck;
		private int _hCropCheck;

		// Are we cropping right now?
		private bool _vCrop;
		private bool _hCrop;

		// Loaded settings
		private bool _cropLetter;
		private bool _cropPillar;
		private int _cropDelay;
		private int _vCropCount;
		private int _hCropCount;
		private Stopwatch _frameWatch;


		// Set this when the sector changes
		private bool _sectorChanged;

		// The width of the border to crop from for LEDs
		private readonly float _borderWidth;
		private readonly float _borderHeight;
		
		private int _previewMode;

		// The current crop mode?
		private List<Color> _colorsLed;
		private List<Color> _colorsSectors;
		public bool DoSave;
		public bool NoImage;
		private const int ScaleHeight = DisplayUtil.CaptureHeight;
		private const int ScaleWidth = DisplayUtil.CaptureWidth;

		private readonly ControlService _controlService;

		
		public Splitter(SystemData sd, ControlService controlService) {
			_frameWatch = new Stopwatch();
			_frameWatch.Start();
			_controlService = controlService;
			_controlService.RefreshSystemEvent += RefreshSystem;
			// Set some defaults, this should probably just not be null
			if (sd != null) {
				_leftCount = sd.LeftCount;
				_topCount = sd.TopCount;
				_rightCount = sd.RightCount == 0 ? _leftCount : sd.RightCount;
				_bottomCount = sd.BottomCount == 0 ? _topCount : sd.BottomCount;
				_hSectors = sd.HSectors;
				_vSectors = sd.VSectors;
				_cropDelay = sd.CropDelay;
				_cropLetter = sd.EnableLetterBox;
				_cropPillar = sd.EnablePillarBox;
			}

			// Set desired width of capture region to 15% total image
			_borderWidth = 10;
			_borderHeight = 10;
			
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

			_controlService.ColorService.Counter.Tick("Splitter");

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

			var ledColors = new List<Color>();
			for (var i = 0; i < _fullCoords.Count; i++) {
				var sub = new Mat(_input, _fullCoords[i]);
				ledColors.Add(GetAverage(sub));
				sub.Dispose();
			}

			var sectorColors = new List<Color>();
			for (var i = 0; i < _fullSectors.Count; i++) {
				var sub = new Mat(_input, _fullSectors[i]);
				sectorColors.Add(GetAverage(sub));
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
			var colBlack = new Bgr(Color.FromArgb(0,0,0,0)).MCvScalar;
			if (_previewMode == 1) {
				for (var i = 0; i < _fullCoords.Count; i++) {
					var col = new Bgr(_colorsLed[i]).MCvScalar;
					CvInvoke.Rectangle(gMat,_fullCoords[i], col, -1, LineType.AntiAlias);
					CvInvoke.Rectangle(gMat,_fullCoords[i],colBlack, 1, LineType.AntiAlias);
				}
			}

			if (_previewMode == 2) {
				for (var i = 0; i < _fullSectors.Count; i++) {
					var s = _fullSectors[i];
					var col = new Bgr(_colorsSectors[i]).MCvScalar;
					CvInvoke.Rectangle(gMat,s, col, -1, LineType.AntiAlias);
					CvInvoke.Rectangle(gMat,s, colBlack, 1, LineType.AntiAlias);
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
			if (avg.Red < 6 && avg.Green < 6 && avg.Blue < 6) return Color.FromArgb(0, 0, 0, 0);
			return Color.FromArgb(255, (int) avg.Red, (int) avg.Green, (int) avg.Blue);
		}

		public List<Color> GetColors() {
			return _colorsLed;
		}

		public List<Color> GetSectors() {
			return _colorsSectors;
		}

		private void CheckSectors() {
			// If nothing to crop, just leave
			if (!_cropLetter && !_cropPillar) return;
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

			var lMin = int.MaxValue;
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
					if (!IsBlack(l1, l2, blackLevel)) break;
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
					if (!IsBlack(n1,n2,blackLevel)) break;
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

			//Log.Debug($"HCC {_hCropCount}, check {_hCropCheck}, hccount {_hCropCount}, current {cropHorizontal}");
			
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
			var image = input.ToImage<Bgr, Byte>();
			var maxR = 0;
			var maxG = 0;
			var maxB = 0;
			for (var r = 0; r < image.Rows; r++) {
				for (var c = 0; c < image.Cols; c++) {
					var col = image[r, c];
					maxR = Math.Max((int)col.Red, maxR);
					maxG = Math.Max((int)col.Green, maxG);
					maxB = Math.Max((int) col.Blue, maxB);
				}
			}

			return Color.FromArgb(maxR, maxG, maxB);
		}

		private static bool IsBlack(Color input, Color input2, int blackLevel) {
			return input.R <= blackLevel && input.B <= blackLevel && input.G <= blackLevel && input2.R <= blackLevel &&
			       input2.B <= blackLevel && input2.G <= blackLevel;
		}

		private List<Rectangle> DrawGrid() {
			var vOffset = _vCropPixels;
			var hOffset = _hCropPixels;
			var output = new List<Rectangle>();

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
			while (pos >= 0) {
				output.Add(new Rectangle((int) rLeft, (int) pos, (int) _borderWidth, (int) heightRight));
				pos -= heightRight;
			}

			if (pos > -.002) {
				output.Add(new Rectangle((int) rLeft, 0, (int) _borderWidth, (int) heightRight));
			}
			
			// Calc top regions, from right to left
			pos = ScaleWidth - widthTop;
			while (pos >= 0) {
				output.Add(new Rectangle((int) pos, tTop, (int) widthTop, (int) _borderHeight));
				pos -= widthTop;
			}
			if (pos > -.002) {
				output.Add(new Rectangle(0, tTop, (int) widthTop, (int) _borderHeight));
			}
			
			// Calc left regions (top to bottom)
			pos = 0;
			while (pos < ScaleHeight) {
				output.Add(new Rectangle(lLeft, (int) pos, (int) _borderWidth, (int) heightLeft));
				pos += heightLeft;
			}
			
			// Calc bottom regions (L-R)
			pos = 0;
			while (pos < ScaleWidth) {
				output.Add(new Rectangle((int) pos, (int) bTop, (int) widthBottom, (int) _borderHeight));
				pos += widthBottom;
			}
			
			return output;
		}


		private List<Rectangle> DrawSectors() {
			// How many sectors does each region have?
			var hOffset = _hCropPixels;
			var vOffset = _vCropPixels;
			if (_hSectors == 0) _hSectors = 10;
			if (_vSectors == 0) _vSectors = 6;
			// This is where we're saving our output
			var fs = new List<Rectangle>();
			// Calculate heights, minus offset for boxing
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
			var step = _vSectors - 1;
			while (step >= 0) {
				var ord = step * sectorHeight + vOffset;
				fs.Add(new Rectangle(minRight, ord, sectorWidth, sectorHeight));
				step--;
			}

			// Calc top regions, from right to left, skipping top-right corner (total horizontal sectors minus one)
			step = _hSectors - 2;
			while (step >= 0) {
				var ord = step * sectorWidth + hOffset;
				fs.Add(new Rectangle(ord, minTop, sectorWidth, sectorHeight));
				step--;
			}

			step = 1;
			// Calc left regions (top to bottom), skipping top-left
			while (step <= _vSectors - 1) {
				var ord = step * sectorHeight + vOffset;
				fs.Add(new Rectangle(minLeft, ord, sectorWidth, sectorHeight));
				step++;
			}

			step = 1;
			// Calc bottom center regions (L-R)
			while (step <= _hSectors - 2) {
				var ord = step * sectorWidth + hOffset;
				fs.Add(new Rectangle(ord, minBot, sectorWidth, sectorHeight));
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
			if (_hSectors == 0) _hSectors = 10;
			if (_vSectors == 0) _vSectors = 6;
			_fullCoords = DrawGrid();
			_fullSectors = DrawSectors();
		}
	}
}