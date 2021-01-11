#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Glimmr.Models.LED;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.Video {
	public class Splitter {
		private Mat _input;
		private readonly int _leftCount;
		private readonly int _topCount;
		private readonly int _rightCount;
		private readonly int _bottomCount;
		private readonly int _sourceWidth;

		private readonly int _sourceHeight;

		private List<Rectangle> _fullCoords;
		private List<Rectangle> _fullSectors;

		// Number of times our counts have matched
		private int _vCropCount;

		private int _hCropCount;

		// Current crop settings
		private int _vCropPixels;

		private int _hCropPixels;

		// Where we save the potential new value between checks
		private int _vCropCheck;

		private int _hCropCheck;

		// Are we cropping right now?
		private bool _vCrop;

		private bool _hCrop;

		// Set this when the sector changes
		private bool _sectorChanged;

		// The width of the border to crop from for LEDs
		private readonly float _borderWidth;

		private readonly float _borderHeight;

		// The current crop mode?
		private List<Color> _colorsLed;
		private List<Color> _colorsSectors;
		private int _frameCount;
		private readonly float _brightBoost;
		private readonly float _saturationBoost;
		private readonly int _minBrightness;
		public bool DoSave;
		public bool NoImage;
		private const int MaxFrameCount = 30;
		private ControlService _controlService;

		public Splitter(LedData ld, ControlService controlService, int srcWidth, int srcHeight) {
			Log.Debug("Initializing splitter, using LED Data: " + JsonConvert.SerializeObject(ld));
			_controlService = controlService;
			// Set some defaults, this should probably just not be null
			if (ld != null) {
				_leftCount = ld.LeftCount;
				_topCount = ld.TopCount;
				_rightCount = ld.RightCount == 0 ? _leftCount : ld.RightCount;

				_bottomCount = ld.BottomCount == 0 ? _topCount : ld.BottomCount;
			}

			_sourceWidth = srcWidth;
			_sourceHeight = srcHeight;
			// Set desired width of capture region to 15% total image
			_borderWidth = 10;
			_borderHeight = 10;
			// Set default box mode to zero, no boxing
			// Set brightness boost, min brightness, sat boost...
			_brightBoost = 0;
			_minBrightness = 90;
			_saturationBoost = .2f;
			// Get sectors
			_fullCoords = DrawGrid();
			_fullSectors = DrawSectors();
			_frameCount = 0;
			Log.Debug("Splitter init complete.");
		}


		public void Update(Mat inputMat) {
			_input = inputMat ?? throw new ArgumentException("Invalid input material.");
			// Don't do anything if there's no frame.
			//Log.Debug("Updating splitter...");
			if (_input.IsEmpty) {
				Log.Debug("SPLITTER: NO INPUT.");
				return;
			}

			var outColorsSector = new List<Color>();
			if (_frameCount >= 10) {
				CheckSectors();
				_frameCount = 0;
			}

			_frameCount++;
			// Only calculate new sectors if the value has changed
			if (_sectorChanged) {
				_sectorChanged = false;
				_fullCoords = DrawGrid();
				_fullSectors = DrawSectors();
			}

			// Save a preview image if desired
			if (DoSave) {
				DoSave = false;
				var path = Directory.GetCurrentDirectory();
				var gMat = new Mat();
				inputMat.CopyTo(gMat);
				gMat.Save(path + "/wwwroot/img/_preview_output.jpg");
				gMat.Dispose();
				_controlService.TriggerImageUpdate();
			}

			var outColorsStrip = _fullCoords.Select(sub => new Mat(_input, sub)).Select(nm => GetAverage(nm)).ToList();

			foreach (var sub in _fullSectors.Select(r => new Mat(_input, r))) {
				outColorsSector.Add(GetAverage(sub));
				sub.Dispose();
			}

			_colorsLed = outColorsStrip;
			_colorsSectors = outColorsSector;
		}


		private static Color GetAverage2(Mat sInput) {
			var outColor = Color.Black;
			if (sInput.Cols == 0) return outColor;
			var colors = CvInvoke.Mean(sInput);
			var cB = (int) colors.V0;
			var cG = (int) colors.V1;
			var cR = (int) colors.V2;
			outColor = Color.FromArgb(cR, cG, cB);
			return outColor;
		}

		private static Color GetAverage(Mat sInput) {
			var output = sInput.ToImage<Bgr, byte>();
			var avg = output.GetAverage();
			return Color.FromArgb(255, (int) avg.Red, (int) avg.Green, (int) avg.Blue);
		}

		public List<Color> GetColors() {
			return _colorsLed;
		}

		public List<Color> GetSectors() {
			return _colorsSectors;
		}

		private void CheckSectors() {
			// Number of pixels to check per loop. Smaller = more accurate, larger = faster?
			const int checkSize = 1;
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

			// Loop through rows, checking for all black
			for (var r = 0; r <= _input.Height / 4; r += checkSize) {
				// Define top position of bottom section
				var r2 = _input.Height - r - checkSize;
				// Top Sector
				var s1 = new Rectangle(0, r, _input.Width, checkSize);
				// Make it a mat and check to see if it's black
				var t1 = new Mat(gr, s1);
				var n1 = CvInvoke.CountNonZero(t1);
				t1.Dispose();
				// If it isn't, we can stop here
				//Log.Debug($"R1 @ {r} is " + n1);                                
				if (n1 >= 5) break;
				// If it is, check the corresponding bottom sector
				var s2 = new Rectangle(0, r2, _input.Width, checkSize);
				var t2 = new Mat(gr, s2);
				n1 = CvInvoke.CountNonZero(t2);
				t2.Dispose();
				// If the bottom is also black, save that value
				if (n1 <= 5) //Log.Debug($"R2 @ {r} is " + n1);
					cropHorizontal = r + 1;
				else
					break;
			}

			for (var c = 0; c < _input.Width / 4; c += checkSize) {
				// Define left coord of right sector
				var c2 = _input.Width - c - checkSize;
				// Create rect for left side check, make it a Mat
				var s1 = new Rectangle(c, 0, checkSize, _input.Height);
				var t1 = new Mat(gr, s1);
				// See if it's all black
				var n1 = CvInvoke.CountNonZero(t1);
				t1.Dispose();
				// If not, stop here
				//Log.Debug($"N2 @ {c} " + n1);
				if (n1 >= 4) break;
				// Otherwise, do the same for the right side
				var s2 = new Rectangle(c2, 0, 1, _input.Height);
				var t2 = new Mat(gr, s2);
				n1 = CvInvoke.CountNonZero(t2);
				t2.Dispose();
				// If it is also black, set that to our current check value
				if (n1 <= 3)
					cropVertical = c;
				else
					break;
			}

			gr.Dispose();
			// If we have a h crop value
			if (cropHorizontal != 0) {
				// Check to see if we have the same crop value
				if (_hCropCheck == cropHorizontal) {
					_hCropCount++;
					// If it is, set to new value
					// Now check to see if we've had the same value N times
					if (_hCropCount > MaxFrameCount) {
						_hCropCount = MaxFrameCount;
						_hCropPixels = _hCropCheck;
						// If we have, set the hCrop value and tell program to crop
						_sectorChanged = true;
						_hCrop = true;
						if (!_hCrop) Log.Debug($"Enabling horizontal crop for {cropHorizontal} rows.");
					}
				} else {
					_hCropCheck = cropHorizontal;
				}
			} else {
				_hCropCount = -1;
			}

			//Log.Debug($"H crop count, new, check: {_hCropCount}, {cropHorizontal}, {_hCropCheck}");

			// If our crop count is lt zero, set it and the crop value to zero
			if (_hCropCount < 0) {
				_hCropPixels = 0;
				_hCropCount = 0;
				_hCropCheck = 0;
				if (_hCrop) {
					_sectorChanged = true;
					_hCrop = false;
					Log.Debug("Disabling horizontal crop.");
				}
			}

			// If we have a vCrop value
			if (cropVertical != 0) {
				// Check to see if we have the same crop value
				if (_vCropCheck == cropVertical) {
					_vCropCount++;
					// If it is, set to new value
					// Now check to see if we've had the same value N times
					if (_vCropCount > MaxFrameCount) {
						_vCropCount = MaxFrameCount;
						_vCropPixels = _vCropCheck;
						// If we have, set the vCrop value and tell program to crop
						_sectorChanged = true;
						_vCrop = true;
						if (!_vCrop) Log.Debug($"Enabling Vertical crop for {cropVertical} rows.");
					}
				} else {
					_vCropCheck = cropVertical;
				}
			} else {
				_vCropCount = -1;
			}

			//Log.Debug($"vCrop count, new, check: {_vCropCount}, {cropVertical}, {_vCropCheck}");

			// If our crop count is lt zero, set it and the crop value to zero
			if (_vCropCount >= 0) return;
			_vCropPixels = 0;
			_vCropCount = 0;
			_vCropCheck = 0;
			if (!_vCrop) return;
			_sectorChanged = true;
			_vCrop = false;
			Log.Debug("Disabling Vertical crop.");
		}


		private List<Rectangle> DrawGrid() {
			var vOffset = _vCropPixels;
			var hOffset = _hCropPixels;
			var output = new List<Rectangle>();

			// Top Region
			var tTop = hOffset;

			// Bottom Region
			var bBottom = _sourceHeight - hOffset;
			var bTop = bBottom - _borderHeight;

			// Left Column Border
			var lLeft = vOffset;

			// Right Column Border
			var rRight = _sourceWidth - vOffset;
			var rLeft = rRight - _borderWidth;

			// Steps
			var widthTop = (float) _sourceWidth / _topCount;
			var widthBottom = (float) _sourceWidth / _bottomCount;
			var heightLeft = (float) _sourceHeight / _leftCount;
			var heightRight = (float) _sourceHeight / _rightCount;

			// Calc right regions, bottom to top
			var pos = _sourceHeight - heightRight;
			while (pos > 0) {
				output.Add(new Rectangle((int) rLeft, (int) pos, (int) _borderWidth, (int) heightRight));
				pos -= heightRight;
			}

			// Calc top regions, from right to left
			pos = _sourceWidth - widthTop;
			while (pos > 0) {
				output.Add(new Rectangle((int) pos, tTop, (int) widthTop, (int) _borderHeight));
				pos -= widthTop;
			}

			// Calc left regions (top to bottom)
			pos = 0;
			while (pos <= _sourceHeight - heightLeft) {
				output.Add(new Rectangle(lLeft, (int) pos, (int) _borderWidth, (int) heightLeft));
				pos += heightLeft;
			}

			// Calc bottom regions (L-R)
			pos = 0;
			while (pos <= _sourceWidth - widthBottom) {
				output.Add(new Rectangle((int) pos, (int) bTop, (int) widthBottom, (int) _borderHeight));
				pos += widthBottom;
			}

			return output;
		}


		private List<Rectangle> DrawSectors(bool v2 = true) {
			// How many sectors does each region have?
			var hOffset = _hCropPixels;
			var vOffset = _vCropPixels;
			var vSectorCount = 3;
			var hSectorCount = 5;

			if (v2) {
				vSectorCount *= 2;
				hSectorCount *= 2;
			}

			// This is where we're saving our output
			var fs = new List<Rectangle>();
			// Calculate heights, minus offset for boxing
			// Individual segment sizes
			var sectorWidth = (_sourceWidth - hOffset * 2) / hSectorCount;
			var sectorHeight = (_sourceHeight - vOffset * 2) / vSectorCount;
			// These are based on the border/strip values
			// Minimum limits for top, bottom, left, right            
			var minTop = vOffset;
			var minBot = _sourceHeight - vOffset - sectorHeight;
			var minLeft = hOffset;
			var minRight = _sourceWidth - hOffset - sectorWidth;
			// Calc right regions, bottom to top
			var step = vSectorCount - 1;
			while (step >= 0) {
				var ord = step * sectorHeight + vOffset;
				fs.Add(new Rectangle(minRight, ord, sectorWidth, sectorHeight));
				step--;
			}

			// Calc top regions, from right to left, skipping top-right corner (total horizontal sectors minus one)
			step = hSectorCount - 2;
			while (step >= 0) {
				var ord = step * sectorWidth + hOffset;
				fs.Add(new Rectangle(ord, minTop, sectorWidth, sectorHeight));
				step--;
			}

			step = 1;
			// Calc left regions (top to bottom), skipping top-left
			while (step <= vSectorCount - 1) {
				var ord = step * sectorHeight + vOffset;
				fs.Add(new Rectangle(minLeft, ord, sectorWidth, sectorHeight));
				step++;
			}

			step = 1;
			// Calc bottom center regions (L-R)
			while (step <= hSectorCount - 2) {
				var ord = step * sectorWidth + hOffset;
				fs.Add(new Rectangle(ord, minBot, sectorWidth, sectorHeight));
				step += 1;
			}

			return fs;
		}

		public void Refresh() {
		}
	}
}