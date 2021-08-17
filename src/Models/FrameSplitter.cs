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
using Emgu.CV.Util;
using Glimmr.Enums;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace Glimmr.Models {
	public class FrameSplitter {
		// Do we send our frame data, or store it?
		public bool DoSend { get; set; }
		public bool SourceActive { get; private set; }
		private readonly float _borderHeight;

		// The width of the border to crop from for LEDs
		private readonly float _borderWidth;
		private readonly ColorService _colorService;
		private readonly Stopwatch _frameWatch;
		private readonly List<VectorOfPoint> _targets;
		private readonly bool _useCrop;
		private int _bottomCount;

		// Loaded data
		private CaptureMode _captureMode;

		// The current crop mode?
		private Color[] _colorsLed;
		private Color[] _colorsSectors;
		private int _cropDelay;

		// Loaded settings
		private bool _cropLetter;
		private bool _cropPillar;

		private bool _doSave;

		private Rectangle[] _fullCoords;
		private Rectangle[] _fullSectors;

		private bool _hCrop;
		private int _hCropCheck;
		private int _hCropCount;
		private int _hCropPixels;
		private int _hSectors;
		private Mat _inFrame;

		private int _ledCount;
		private int _leftCount;
		private VectorOfPointF? _lockTarget;
		private DeviceMode _mode;
		private bool _noImage;
		private Mat _outFrame;

		private int _previewMode;
		private int _rightCount;
		private const int ScaleHeight = 480;

		private Size _scaleSize;
		private const int ScaleWidth = 640;

		// Set this when the sector changes
		private bool _sectorChanged;
		private int _sectorCount;
		private int _srcArea;
		private int _topCount;
		private bool _useCenter;

		// Are we cropping right now?
		private bool _vCrop;

		// Where we save the potential new value between checks
		private int _vCropCheck;
		private int _vCropCount;

		// Current crop settings
		private int _vCropPixels;

		// Source stuff
		private PointF[] _vectors;
		private int _vSectors;

		private bool _warned;


		public FrameSplitter(ColorService cs, bool crop = false) {
			_vectors = Array.Empty<PointF>();
			_targets = new List<VectorOfPoint>();
			_useCrop = crop;
			_inFrame = new Mat();
			_outFrame = new Mat();
			_colorsLed = Array.Empty<Color>();
			_colorsSectors = Array.Empty<Color>();
			_frameWatch = new Stopwatch();
			_frameWatch.Start();
			_colorService = cs;
			cs.ControlService.RefreshSystemEvent += RefreshSystem;
			cs.FrameSaveEvent += TriggerSave;
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
			_mode = (DeviceMode) sd.DeviceMode;
			if (!_cropLetter || !_useCrop) {
				_vCrop = false;
				_vCropCheck = 0;
				_vCropPixels = 0;
				_vCropCount = 0;
				_cropLetter = false;
			}

			if (!_cropPillar || !_useCrop) {
				_hCrop = false;
				_hCropCheck = 0;
				_hCropPixels = 0;
				_hCropCount = 0;
				_cropPillar = false;
			}

			_useCenter = sd.UseCenter;
			_ledCount = sd.LedCount;
			_sectorCount = sd.SectorCount;
			_sectorChanged = true;
			if (_ledCount == 0) {
				_ledCount = 200;
			}

			if (_sectorCount == 0) {
				_sectorCount = 12;
			}

			_colorsLed = ColorUtil.EmptyColors(_ledCount);
			_colorsSectors = ColorUtil.EmptyColors(_sectorCount);

			_previewMode = sd.PreviewMode;

			_captureMode = (CaptureMode) sd.CaptureMode;
			_srcArea = ScaleWidth * ScaleHeight;
			_scaleSize = new Size(ScaleWidth, ScaleHeight);

			if (_captureMode == CaptureMode.Camera) {
				try {
					var lt = DataUtil.GetItem<PointF[]>("LockTarget");
					if (lt != null) {
						_lockTarget = new VectorOfPointF(lt);
						var lC = 0;
						while (lC < 20) {
							_targets.Add(VPointFToVPoint(_lockTarget));
							lC++;
						}
					}
				} catch (Exception e) {
					Log.Warning("Video Capture Exception: " + e.Message + " at " + e.StackTrace);
				}
			}

			_vectors = new PointF[] {
				new Point(0, 0), new Point(ScaleWidth, 0), new Point(ScaleWidth, ScaleHeight),
				new Point(0, ScaleHeight)
			};

			// Start our stopwatches for cropping if they were previously disabled
			if (_cropLetter || _cropPillar && !_frameWatch.IsRunning) {
				_frameWatch.Restart();
			}

			// If not cropping, then we don't need a stopwatch
			if (!_cropLetter && !_cropPillar) {
				_frameWatch.Stop();
			}

			_doSave = true;
		}

		private void TriggerSave() {
			_doSave = true;
		}

		private void SaveFrames(Mat inMat, Mat outMat) {
			_doSave = false;
			if (inMat != null && !inMat.IsEmpty) {
				_colorService.ControlService.SendImage("inputImage", inMat).ConfigureAwait(false);
			}
			
			if (outMat == null || outMat.IsEmpty) {
				return;
			}

			var colBlack = new Bgr(Color.FromArgb(0, 0, 0, 0)).MCvScalar;
			switch (_previewMode) {
				case 1: {
					for (var i = 0; i < _fullCoords.Length; i++) {
						var col = new Bgr(_colorsLed[i]).MCvScalar;
						CvInvoke.Rectangle(outMat, _fullCoords[i], col, -1, LineType.AntiAlias);
						CvInvoke.Rectangle(outMat, _fullCoords[i], colBlack, 1, LineType.AntiAlias);
					}
					break;
				}
				case 2: {
					for (var i = 0; i < _fullSectors.Length; i++) {
						var s = _fullSectors[i];
						var col = new Bgr(_colorsSectors[i]).MCvScalar;
						CvInvoke.Rectangle(outMat, s, col, -1, LineType.AntiAlias);
						CvInvoke.Rectangle(outMat, s, colBlack, 1, LineType.AntiAlias);
						var cInt = i + 1;
						var tPoint = new Point(s.X, s.Y + 30);
						CvInvoke.PutText(outMat, cInt.ToString(), tPoint, FontFace.HersheySimplex, 0.75, colBlack);
					}
					break;
				}
			}

			if (DoSend) {
				_colorService.ControlService.SendImage("outputImage", outMat).ConfigureAwait(false);
			}
			inMat?.Dispose();
			outMat.Dispose();
		}

		public async Task MergeFrame(Color[] leds, Color[] sectors) {
			await Task.Delay(5);
			var path = Directory.GetCurrentDirectory();
			var inPath = Path.Join(path, "wwwroot", "img", "_preview_input.jpg");
			var outPath = Path.Join(path, "wwwroot", "img", "_preview_output.jpg");
			if (_inFrame == null || _outFrame == null) {
				return;
			}

			if (_inFrame.IsEmpty || _outFrame.IsEmpty) {
				return;
			}

			_inFrame.Save(inPath);
			var outMat = new Mat();
			_outFrame.CopyTo(outMat);
			var colBlack = new Bgr(Color.FromArgb(0, 0, 0, 0)).MCvScalar;
			if (_previewMode == 1) {
				for (var i = 0; i < _fullCoords.Length; i++) {
					var col = new Bgr(leds[i]).MCvScalar;
					CvInvoke.Rectangle(outMat, _fullCoords[i], col, -1, LineType.AntiAlias);
					CvInvoke.Rectangle(outMat, _fullCoords[i], colBlack, 1, LineType.AntiAlias);
				}
			}

			if (_previewMode == 2) {
				for (var i = 0; i < _fullSectors.Length; i++) {
					var s = _fullSectors[i];
					var col = new Bgr(sectors[i]).MCvScalar;
					CvInvoke.Rectangle(outMat, s, col, -1, LineType.AntiAlias);
					CvInvoke.Rectangle(outMat, s, colBlack, 1, LineType.AntiAlias);
					var cInt = i + 1;
					var tPoint = new Point(s.X, s.Y + 30);
					CvInvoke.PutText(outMat, cInt.ToString(), tPoint, FontFace.HersheySimplex, 1.0, colBlack);
				}
			}

			outMat.Save(outPath);
			outMat.Dispose();
		}

		
		public async Task Update(Mat frame) {
			if (frame == null || frame.IsEmpty) {
				SourceActive = false;
				if (!_warned) {
					Log.Warning("Frame is null.");
				}
				_warned = true;
				return;
			}

			if (frame.Cols == 0) {
				SourceActive = false;
				if (!_warned) {
					Log.Warning("Frame has no columns.");
				}
				_warned = true;
				return;
			}

			if (frame.Rows == 0) {
				SourceActive = false;
				if (!_warned) {
					Log.Warning("Frame has no rows.");
				}
				_warned = true;
				return;
			}


			var clone = frame;
			if (_captureMode == CaptureMode.Camera && _mode == DeviceMode.Video) {
				clone = CheckCamera(frame);
				if (clone == null || clone.IsEmpty || clone.Cols == 0) {
					Log.Warning("Invalid input frame.");
					return;
				}
			}
			// Check if we're using camera/video mode and crop if necessary
			SourceActive = true;
			
			// Don't do anything if there's no frame.
			if (clone == null || clone.IsEmpty) {
				Log.Warning("Null/Empty input!");
				// Dispose frame
				frame.Dispose();
				clone?.Dispose();
				return;
			}

			// Check sectors once per second
			if (_frameWatch.Elapsed >= TimeSpan.FromSeconds(1)) {
				await CheckCrop(clone).ConfigureAwait(false);
				_frameWatch.Restart();
			}
			

			var ledColors = ColorUtil.EmptyColors(_ledCount);
			for (var i = 0; i < _fullCoords.Length; i++) {
				var sub = new Mat(clone, _fullCoords[i]);
				ledColors[i] = GetAverage(sub);
				sub.Dispose();
			}

			
			var sectorColors = ColorUtil.EmptyColors(_sectorCount);
			for (var i = 0; i < _fullSectors.Length; i++) {
				var sub = new Mat(clone, _fullSectors[i]);
				sectorColors[i] = GetAverage(sub);
				sub.Dispose();
			}

			_colorsLed = ledColors;
			_colorsSectors = sectorColors;
			
			if (_doSave) {
				if (DoSend) {
					SaveFrames(frame, clone);
				}
				_doSave = false;
			}
			// Dispose
			frame.Dispose();
			clone.Dispose();
			await Task.FromResult(true);
		}


		private Mat? CheckCamera(Mat input) {
			var scaled = input.Clone();
			
			Mat? output = null;

			// If we don't have a target, find one
			if (_lockTarget == null) {
				_lockTarget = FindTarget(scaled);
				if (_lockTarget != null) {
					Log.Debug("Target hit.");
					DataUtil.SetItem("LockTarget", _lockTarget.ToArray());
				} else {
					Log.Debug("No target.");
				}
			}

			// If we do or we found one...crop it out
			if (_lockTarget != null) {
				var dPoints = _lockTarget.ToArray();
				var warpMat = CvInvoke.GetPerspectiveTransform(dPoints, _vectors);
				output = new Mat();
				CvInvoke.WarpPerspective(scaled, output, warpMat, _scaleSize);
				warpMat.Dispose();
			}

			scaled.Dispose();
			// Once we have a warped frame, we need to do a check every N seconds for letterboxing...
			return output;
		}

		private VectorOfPointF? FindTarget(Mat input) {
			var cannyEdges = new Mat();
			var uImage = new Mat();
			var gray = new Mat();
			var blurred = new Mat();

			// Convert to greyscale
			CvInvoke.CvtColor(input, uImage, ColorConversion.Bgr2Gray);
			CvInvoke.BilateralFilter(uImage, gray, 11, 17, 17);
			uImage.Dispose();
			CvInvoke.MedianBlur(gray, blurred, 11);
			gray.Dispose();
			// Get edged version
			const double cannyThreshold = 0.0;
			const double cannyThresholdLinking = 200.0;
			CvInvoke.Canny(blurred, cannyEdges, cannyThreshold, cannyThresholdLinking);
			blurred.Dispose();

			// Get contours
			using (var contours = new VectorOfVectorOfPoint()) {
				CvInvoke.FindContours(cannyEdges, contours, null, RetrType.List,
					ChainApproxMethod.ChainApproxSimple);
				var count = contours.Size;
				// Looping contours
				for (var i = 0; i < count; i++) {
					var approxContour = new VectorOfPoint();
					using var contour = contours[i];
					CvInvoke.ApproxPolyDP(contour, approxContour, CvInvoke.ArcLength(contour, true) * 0.02,
						true);
					if (approxContour.Size != 4) {
						continue;
					}

					var cntArea = CvInvoke.ContourArea(approxContour);
					if (!(cntArea / _srcArea > .15)) {
						continue;
					}

					var pointOut = new VectorOfPointF(SortPoints(approxContour));
					_targets.Add(VPointFToVPoint(pointOut));
				}
			}

			var output = CountTargets(_targets);
			cannyEdges.Dispose();
			return output;
		}

		private VectorOfPointF? CountTargets(IReadOnlyCollection<VectorOfPoint> inputT) {
			VectorOfPointF? output = null;
			var x1 = 0;
			var x2 = 0;
			var x3 = 0;
			var x4 = 0;
			var y1 = 0;
			var y2 = 0;
			var y3 = 0;
			var y4 = 0;
			var iCount = inputT.Count;
			foreach (var point in inputT) {
				x1 += point[0].X;
				y1 += point[0].Y;
				x2 += point[1].X;
				y2 += point[1].Y;
				x3 += point[2].X;
				y3 += point[2].Y;
				x4 += point[3].X;
				y4 += point[3].Y;
			}

			if (iCount > 10) {
				x1 /= iCount;
				x2 /= iCount;
				x3 /= iCount;
				x4 /= iCount;
				y1 /= iCount;
				y2 /= iCount;
				y3 /= iCount;
				y4 /= iCount;

				PointF[] avgPoints = {new(x1, y1), new(x2, y2), new(x3, y3), new(x4, y4)};
				var avgVector = new VectorOfPointF(avgPoints);
				if (iCount > 20) {
					output = avgVector;
				}
			}

			if (iCount > 200) {
				_targets.RemoveRange(0, 150);
			}

			return output;
		}

		private static VectorOfPoint VPointFToVPoint(VectorOfPointF input) {
			var ta = input.ToArray();
			var pIn = new Point[input.Size];
			for (var i = 0; i < ta.Length; i++) {
				pIn[i] = new Point((int) ta[i].X, (int) ta[i].Y);
			}

			return new VectorOfPoint(pIn);
		}


		private static PointF[] SortPoints(VectorOfPoint wTarget) {
			var ta = wTarget.ToArray();
			var pIn = new PointF[wTarget.Size];
			for (var i = 0; i < ta.Length; i++) {
				pIn[i] = ta[i];
			}

			// Order points?
			var tPoints = pIn.OrderBy(p => p.Y);
			var vPoints = pIn.OrderByDescending(p => p.Y);
			var vtPoints = tPoints.Take(2);
			var vvPoints = vPoints.Take(2);
			vtPoints = vtPoints.OrderBy(p => p.X);
			vvPoints = vvPoints.OrderByDescending(p => p.X);
			var pointFs = vtPoints as PointF[] ?? vtPoints.ToArray();
			var tl = pointFs[0];
			var tr = pointFs[1];
			var enumerable = vvPoints as PointF[] ?? vvPoints.ToArray();
			var br = enumerable[0];
			var bl = enumerable[1];
			PointF[] outPut = {tl, tr, br, bl};
			return outPut;
		}


		private static Color GetAverage(IInputArray sInput) {
			var foo = CvInvoke.Mean(sInput);
			var red = foo.V2;
			var green = foo.V1;
			var blue = foo.V0;
			if (red < 6 && green < 6 && blue < 6) {
				return Color.FromArgb(0, 0, 0, 0);
			}

			return Color.FromArgb(255, (int) red, (int) green, (int) blue);
		}

		public Color[] GetColors() {
			return _colorsLed;
		}

		public Color[] GetSectors() {
			return _colorsSectors;
		}

		private async Task CheckCrop(Mat image) {
			var width = image.Width;
			var height = image.Height;
			var wStart = width / 3;
			var hStart = height / 3;
			var wEnd = wStart * 2;
			var hEnd = hStart * 2;
			var xCenter = width / 2;
			var yCenter = height / 2;
			var blackLevel = 1;

			var cropVertical = 0;
			var cropHorizontal = 0;

			width--; // remove 1 pixel to get end pixel index
			height--;
			
			var gr = new Mat();
			CvInvoke.CvtColor(image, gr, ColorConversion.Bgr2Gray);
			// Check to see if everything is black
			var allB = CvInvoke.CountNonZero(gr);
			_noImage = allB == 0;
			// If it is, we can stop here
			if (_noImage) {
				gr.Dispose();
				return;
			}

			// find first X pixel of the image
			if (_cropLetter) {
				for (var x = 0; x < wStart; ++x) {
					var c1 = gr.Row(yCenter).Col(width-x);
					var c2 = gr.Row(hStart).Col(x);
					var c3 = gr.Row(hEnd).Col(x);
					var l1 = c1.GetRawData()[0];
					var l2 = c2.GetRawData()[0];
					var l3 = c3.GetRawData()[0];
					if (l1+l2+l3 <= blackLevel * 3) {
						cropVertical = x;
					} else {
						break;
					}
				}
			}

			// find first Y pixel of the image
			if (_cropPillar) {
				for (var y = 0; y < hStart; y++) {
					var c1 = gr.Row(height - y).Col(xCenter);
					var c2 = gr.Row(y).Col(wStart);
					var c3 = gr.Row(y).Col(wEnd);
					var l1 = (int)c1.GetRawData()[0];
					var l2 = (int)c2.GetRawData()[0];
					var l3 = (int)c3.GetRawData()[0];
					c1.Dispose();
					c2.Dispose();
					c3.Dispose();
					if (l1+l2+l3 <= blackLevel * 3) {
						cropHorizontal = y;
					} else {
						break;
					}
				}

			}
			
			gr.Dispose();

			
			if (_cropLetter && cropHorizontal != 0) {
				if (cropHorizontal == _hCropCheck) {
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
				if (cropVertical == _vCropCheck) {
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
			// Only calculate new sectors if the value has changed
			if (_sectorChanged) {
				Log.Debug($"Sector changed, redrawing {_vCropPixels} and {_hCropPixels}...");
				_sectorChanged = false;
				_fullCoords = DrawGrid();
				_fullSectors = DrawSectors();
			}

			await Task.FromResult(true);
		}


		private static Color GetBrightestColor(Mat input) {
			var bytes = input.GetRawData();
			var image = input.ToImage<Bgr, byte>();
			var maxR = 0;
			var maxG = 0;
			var maxB = 0;
			Log.Debug("GBC");
			for (var r = 0; r < image.Rows; r++) {
				for (var c = 0; c < image.Cols; c++) {
					Log.Debug("Looping...");
					var col = image[r, c];
					maxR = Math.Max((int) col.Red, maxR);
					maxG = Math.Max((int) col.Green, maxG);
					maxB = Math.Max((int) col.Blue, maxB);
				}
			}
			Log.Debug($"GBCd: {maxR}, {maxG}, {maxB}:" + (int)bytes[0]);

			return Color.FromArgb(maxR, maxG, maxB);
		}

		private static bool IsBlack(Color input, Color input2, Color input3, int blackLevel) {
			var v1 = input.R + input.G + input.B;
			var v2 = input2.R + input2.G + input2.B;
			var v3 = input3.R + input3.G + input3.B;
			return v1 + v2 + v3 < blackLevel * 9;
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

				output[idx] = new Rectangle((int) rLeft, pos, (int) _borderWidth, heightRight);
				pos -= heightRight;
				idx++;
			}

			// Calc top regions, from right to left
			pos = ScaleWidth - widthTop;

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
				if (pos > ScaleHeight - heightLeft) {
					pos = ScaleHeight - heightLeft;
				}

				output[idx] = new Rectangle(lLeft, pos, (int) _borderWidth, heightLeft);
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

				if (pos > ScaleWidth - widthBottom) {
					pos = ScaleWidth - widthBottom;
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
			var sectorWidth = (ScaleWidth - hOffset * 2) / _hSectors;
			var sectorHeight = (ScaleHeight - vOffset * 2) / _vSectors;
			// These are based on the border/strip values
			// Minimum limits for top, bottom, left, right            
			var minTop = vOffset;
			var minBot = ScaleHeight - vOffset - squareSize;
			var minLeft = hOffset;
			var minRight = ScaleWidth - hOffset - squareSize;
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
	}
}