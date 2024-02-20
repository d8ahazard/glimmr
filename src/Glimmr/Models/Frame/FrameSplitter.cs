#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.IntensityTransform;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Glimmr.Enums;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;
using static Glimmr.Models.Constant.GlimmrConstants;

#endregion

namespace Glimmr.Models.Frame;

public class FrameSplitter : IDisposable {
	// Do we send our frame data to the UI?
	public bool DoSend {
		set {
			_doSend = value;
			if (_doSend) {
				_lFrameCropTrigger.Clear();
				_pFrameCropTrigger.Clear();
				_cropTimer.Start();
			} else {
				_cropTimer.Stop();
			}
		}
	}

	public bool SourceActive { get; set; }
	private ColorService ColorService { get; }
	private readonly float _borderHeight;

	// The width of the border to crop from for LEDs
	private readonly float _borderWidth;
	private readonly Timer _cropTimer;
	private readonly List<VectorOfPoint> _targets;
	private readonly bool _useCrop;
	private bool _allBlack;
	private int _blackLevel;
	private int _bottomCount;

	// Loaded data
	private CaptureMode _captureMode;
	private bool _checkCrop;

	// The current crop mode?
	private Color[] _colorsLed;
	private Color[] _colorsLedIn;
	private Color[] _colorsSectors;
	private Color[] _colorsSectorsIn;
	private bool _correctGamma;
	private int _cropBlackLevel;
	private int _cropDelay;

	// Loaded settings
	private bool _cropLetter;
	private bool _cropPillar;

	private bool _doSave;

	private bool _doSend;
	private Color[] _empty;
	private Color[] _emptySectors;

	private Rectangle[] _fullCoords;
	private Rectangle[] _fullSectors;
	private float _gammaCorrection;
	private int _hSectors;

	// Track crop state
	private bool _lCrop;
	private bool _pCrop;
	
	private int _lCropPixels;

	private int _ledCount;
	private int _leftCount;

	// Current crop data
	private FrameCropTrigger _lFrameCropTrigger;
	private VectorOfPointF? _lockTarget;
	private bool _merge;
	private DeviceMode _mode;
	private int _pCropPixels;
	private FrameCropTrigger _pFrameCropTrigger;


	private int _previewMode;
	private int _rightCount;
	private Size _scaleSize;

	// Set this when the sector changes
	private bool _sectorChanged;
	private int _sectorCount;
	private int _srcArea;
	private int _topCount;
	private bool _useCenter;


	// Source stuff
	private PointF[] _vectors;
	private int _vSectors;
	private bool _warned;

	public FrameSplitter(ColorService cs, bool crop = false) {
		_vectors = Array.Empty<PointF>();
		_targets = new List<VectorOfPoint>();
		_useCrop = crop;
		_colorsLed = Array.Empty<Color>();
		_colorsSectors = Array.Empty<Color>();
		_colorsLedIn = _colorsLed;
		_colorsSectorsIn = _colorsSectors;
		ColorService = cs;
		_empty = Array.Empty<Color>();
		_emptySectors = _empty;
		var sd = DataUtil.GetSystemData();
		_gammaCorrection = sd.GammaCorrection;
		_correctGamma = _useCrop && _gammaCorrection > 1;
		_cropDelay = sd.CropDelay;
		cs.ControlService.RefreshSystemEvent += RefreshSystem;
		cs.FrameSaveEvent += TriggerSave;
		_pFrameCropTrigger = new FrameCropTrigger(_cropDelay);
		_lFrameCropTrigger = new FrameCropTrigger(_cropDelay);
		RefreshSystem();
		// Set desired width of capture region to 15% total image
		_borderWidth = 10;
		_borderHeight = 10;
		// Get sectors
		_fullCoords = DrawGrid();
		_fullSectors = DrawSectors();
		_cropTimer = new Timer(1000);
		_cropTimer.Elapsed += TriggerCropCheck;
	}

	public void Dispose() {
		_lockTarget?.Dispose();
		_cropTimer.Dispose();
		GC.SuppressFinalize(this);
	}

	private void TriggerCropCheck(object? sender, ElapsedEventArgs e) {
		_checkCrop = true;
	}


	private void RefreshSystem() {
		var sd = DataUtil.GetSystemData();
		_gammaCorrection = sd.GammaCorrection;
		_correctGamma = _useCrop && _gammaCorrection > 1;
		_blackLevel = sd.BlackLevel;
		_cropBlackLevel = sd.CropBlackLevel;
		_leftCount = sd.LeftCount;
		_topCount = sd.TopCount;
		_rightCount = sd.RightCount;
		_bottomCount = sd.BottomCount;
		_hSectors = sd.HSectors;
		_vSectors = sd.VSectors;
		_cropDelay = sd.CropDelay;
		_pFrameCropTrigger = new FrameCropTrigger(_cropDelay);
		_lFrameCropTrigger = new FrameCropTrigger(_cropDelay);
		_cropLetter = sd.EnableLetterBox;
		_cropPillar = sd.EnablePillarBox;
		_mode = sd.DeviceMode;
		if (!_cropLetter || !_useCrop) {
			_lCropPixels = 0;
			_cropLetter = false;
		}

		if (!_cropPillar || !_useCrop) {
			_pCropPixels = 0;
			_cropPillar = false;
		}

		_useCenter = sd.UseCenter;
		_ledCount = sd.LedCount;
		_empty = ColorUtil.EmptyColors(_ledCount);

		_sectorCount = sd.SectorCount;
		_emptySectors = ColorUtil.EmptyColors(_sectorCount);

		if (_ledCount == 0) {
			_ledCount = 200;
		}

		if (_sectorCount == 0) {
			_sectorCount = 12;
		}

		_colorsLed = ColorUtil.EmptyColors(_ledCount);
		_colorsSectors = ColorUtil.EmptyColors(_sectorCount);

		_previewMode = sd.PreviewMode;
		_captureMode = sd.CaptureMode;
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

		_fullCoords = DrawGrid();
		_fullSectors = DrawSectors();
		_doSave = true;
	}

	private void TriggerSave() {
		if (!_doSend) {
			return;
		}

		_doSave = true;
	}

	
	public void MergeFrame(Color[] leds, Color[] sectors) {
		_colorsLedIn = leds;
		_colorsSectorsIn = sectors;
		_merge = true;
		_doSave = true;
	}

	public async Task<(Color[], Color[])> Update(Mat? frame)
	{
		if (frame == null || frame.IsEmpty || frame.Cols == 0 || frame.Rows == 0)
		{
			SourceActive = false;
			LogWarningOnce("Frame is null or empty.");
			return (Array.Empty<Color>(), Array.Empty<Color>());
		}

		var processedFrame = ProcessFrame(frame);

		if (processedFrame == null || processedFrame.IsEmpty)
		{
			Log.Warning("Processed frame is null or empty.");
			return (Array.Empty<Color>(), Array.Empty<Color>());
		}

		if (_useCrop && _checkCrop)
		{
			await CheckCrop(processedFrame).ConfigureAwait(false);
			_checkCrop = false;
		}

		var (ledColors, sectorColors) = ComputeColors(processedFrame);

		_colorsLed = ledColors;
		_colorsSectors = sectorColors;

		if (_doSend)
		{
			await ColorService.SendColors(ledColors, sectorColors);
		}

		HandleSaving(frame, processedFrame);

		// Cleanup
		CleanupResources(frame, processedFrame);

		return (_colorsLed, _colorsSectors);
	}

	private void LogWarningOnce(string message)
	{
		if (!_warned)
		{
			Log.Warning(message);
			_warned = true;
		}
	}

	private Mat ProcessFrame(Mat frame)
	{
		var clone = ShouldResize(frame) ? ResizeFrame(frame) : frame;

		if (_captureMode == CaptureMode.Camera && _mode == DeviceMode.Video)
		{
			clone = CheckCamera(clone);
		}

		return ApplyGammaCorrectionIfNeeded(clone);
	}

	private static bool ShouldResize(Mat frame)
	{
		return frame.Width != ScaleWidth || frame.Height != ScaleHeight;
	}

	private static Mat ResizeFrame(Mat frame)
	{
		var resized = new Mat();
		CvInvoke.Resize(frame, resized, new Size(ScaleWidth, ScaleHeight));
		return resized;
	}

	private Mat ApplyGammaCorrectionIfNeeded(Mat frame)
	{
		if (_correctGamma)
		{
			var corrected = new Mat();
			IntensityTransformInvoke.GammaCorrection(frame, corrected, _gammaCorrection);
			return corrected;
		}
		return frame;
	}

	private (Color[], Color[]) ComputeColors(Mat frame)
	{
		if (_allBlack) return (_empty, _emptySectors);

		var ledColors = new Color[_fullCoords.Length];
		var sectorColors = new Color[_fullSectors.Length];

		Parallel.For(0, _fullCoords.Length, i =>
		{
			using (var sub = new Mat(frame, _fullCoords[i]))
			{
				ledColors[i] = GetAverage(sub);
			}
		});

		Parallel.For(0, _fullSectors.Length, i =>
		{
			using (var sub = new Mat(frame, _fullSectors[i]))
			{
				sectorColors[i] = GetAverage(sub);
			}
		});

		return (ledColors, sectorColors);
	}

	private void HandleSaving(Mat original, Mat processed)
	{
		if (_doSave && (_doSend || _merge) && ColorService.ControlService.SendPreview)
		{
			SaveFrames(original, processed);
			_doSave = false;
			_merge = false;
		}
	}

	private static void CleanupResources(Mat original, Mat processed)
	{
		original.Dispose();
		if (processed != original) processed.Dispose();
	}

	
	private Color GetAverage(IInputArray sInput) {
		var foo = CvInvoke.Mean(sInput);
		var red = (int)foo.V2;
		var green = (int)foo.V1;
		var blue = (int)foo.V0;
		if (red < _blackLevel && green < _blackLevel && blue < _blackLevel) {
			return Color.FromArgb(0, 0, 0, 0);
		}

		return Color.FromArgb(red, green, blue);
	}

	
	private void SaveFrames(Mat inMat, Mat outMat) {
		_doSave = false;
		var cols = _colorsLed;
		var secs = _colorsSectors;
		if (_merge) {
			cols = _colorsLedIn;
			secs = _colorsSectorsIn;
		}
		if (inMat is { IsEmpty: false }) {
			ControlService.SendImage("inputImage", inMat).ConfigureAwait(false);
		}

		if (outMat.IsEmpty) {
			Log.Debug("Output image is empty.");
			return;
		}
		var colBlack = new Bgr(Color.FromArgb(255, 128, 128, 128)).MCvScalar;
		switch (_previewMode) {
			
			case 1: {
				for (var i = 0; i < _fullCoords.Length; i++) {
					var color = cols[i];
					if (color.R < _blackLevel && color.G < _blackLevel && color.B < _blackLevel) {
						color = Color.Black;
					}

					var col = new Bgr(color).MCvScalar;
					CvInvoke.Rectangle(outMat, _fullCoords[i], col, -1, LineType.AntiAlias);
					CvInvoke.Rectangle(outMat, _fullCoords[i], colBlack, 1, LineType.AntiAlias);
				}
				break;
			}
			case 2: {
				for (var i = 0; i < _fullSectors.Length; i++) {
					var s = _fullSectors[i];
					var color = secs[i];
					if (color.R < _blackLevel && color.G < _blackLevel && color.B < _blackLevel) {
						color = Color.FromArgb(0, 0, 0);
					}

					var col = new Bgr(color).MCvScalar;
					CvInvoke.Rectangle(outMat, s, col, -1, LineType.AntiAlias);
					CvInvoke.Rectangle(outMat, s, colBlack, 1, LineType.AntiAlias);
					var cInt = i + 1;
					var tPoint = new Point(s.X, s.Y + 30);
					CvInvoke.PutText(outMat, cInt.ToString(), tPoint, FontFace.HersheySimplex, 0.75, colBlack);
				}
				break;
			}
		}

		if (outMat is { IsEmpty: false }) {
			ControlService.SendImage("outputImage", outMat).ConfigureAwait(false);
		}

		inMat.Dispose();
		outMat.Dispose();
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

	private VectorOfPointF? FindTarget(IInputArray input) {
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

			PointF[] avgPoints = { new(x1, y1), new(x2, y2), new(x3, y3), new(x4, y4) };
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
			pIn[i] = new Point((int)ta[i].X, (int)ta[i].Y);
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
		PointF[] outPut = { tl, tr, br, bl };
		return outPut;
	}

	
	public Color[] GetColors() {
		return _colorsLed;
	}

	public Color[] GetSectors() {
		return _colorsSectors;
	}
	
	private async Task CheckCropOg(Mat image) {
		// Set our tolerances
		_sectorChanged = false;
		var width = ScaleWidth;
		var height = ScaleHeight;
		var wMax = width / 3;
		var hMax = height / 3;
		// How many non-black pixels can be in a given row
		var lPixels = 0;
		var pPixels = 0;

		width--;
		height--;
		var raw = image.GetRawData();
		var unique = raw.Distinct().ToArray();

		//var count = Sum(raw);
		var noImage = width == 0 || height == 0 || (unique.Length == 1 && unique[0] <= _cropBlackLevel);
		// If it is, we can stop here
		if (noImage) {
			_allBlack = true;
			if (_doSend) {
				ColorService.CheckAutoDisable(false);
			}

			return;
		}

		if (_doSend) {
			ColorService.CheckAutoDisable(true);
		}

		// Return here, because otherwise, "no input" detection won't work.
		if (!_useCrop) {
			return;
		}

		// Convert image to greyscale

		_allBlack = false;
		// Check letterboxing
		if (_cropLetter) {
			for (var y = 0; y < hMax; y += 2) {
				var c1 = image.Row(height - y);
				var c2 = image.Row(y);
				var b1 = c1.GetRawData().SkipLast(8).Skip(8).ToArray();
				var b2 = c2.GetRawData().SkipLast(8).Skip(8).ToArray();
				var l1 = Sum(b1) / b1.Length;
				var l2 = Sum(b2) / b2.Length;
				c1.Dispose();
				c2.Dispose();
				if (l1 <= _cropBlackLevel && l2 <= _cropBlackLevel && l1 == l2) {
					lPixels = y;
				} else {
					break;
				}
			}

			_lFrameCropTrigger.Tick(lPixels);
			if (_lFrameCropTrigger.Triggered != _lCrop && !_sectorChanged) {
				_sectorChanged = true;
				_lCrop = _lFrameCropTrigger.Triggered;
			}

			_lCropPixels = lPixels;
		}

		// Check pillarboxing
		if (_cropPillar) {
			for (var x = 0; x < wMax; x += 2) {
				var c1 = image.Col(width - x);
				var c2 = image.Col(x);
				var b1 = c1.GetRawData().SkipLast(8).Skip(8).ToArray();
				var b2 = c2.GetRawData().SkipLast(8).Skip(8).ToArray();
				var l1 = Sum(b1) / b1.Length;
				var l2 = Sum(b2) / b2.Length;
				c1.Dispose();
				c2.Dispose();
				if (l1 <= _cropBlackLevel && l2 <= _cropBlackLevel && l1 == l2) {
					pPixels = x;
				} else {
					break;
				}
			}

			_pFrameCropTrigger.Tick(pPixels);
			if (_pFrameCropTrigger.Triggered != _pCrop && !_sectorChanged) {
				_sectorChanged = true;
				_pCrop = _pFrameCropTrigger.Triggered;
			}

			_pCropPixels = pPixels;
		}

		// Cleanup mat
		//image.Dispose();

		// Only calculate new sectors if the value has changed
		if (_sectorChanged) {
			_fullCoords = DrawGrid();
			_fullSectors = DrawSectors();
			_sectorChanged = false;
		}

		await Task.FromResult(true);
	}

	private async Task CheckCrop(Mat image)
	{
		// Set our tolerances
		_sectorChanged = false;
		var width = ScaleWidth - 1; // Adjusted here to avoid decrementing later
		var height = ScaleHeight - 1; // Adjusted here to avoid decrementing later
		var wMax = width / 3;
		var hMax = height / 3;

		// Early check for no image condition
		if (width <= 0 || height <= 0)
		{
			_allBlack = true;
			if (_doSend)
			{
				ColorService.CheckAutoDisable(false);
			}
			return;
		}

		if (_doSend)
		{
			ColorService.CheckAutoDisable(true);
		}

		if (!_useCrop)
		{
			return;
		}

		_allBlack = false;
		var lPixels = 0;
		var pPixels = 0;

		// Process for letterboxing
		if (_cropLetter)
		{
			for (var y = 0; y < hMax; y += 2)
			{
				var avgTop = GetRowAverage(image, y, width);
				var avgBottom = GetRowAverage(image, height - y, width);
				if (avgTop <= _cropBlackLevel && avgBottom <= _cropBlackLevel && avgTop == avgBottom)
				{
					lPixels = y;
				}
				else
				{
					break;
				}
			}

			UpdateCrop(ref _lCrop, ref _lCropPixels, lPixels, _lFrameCropTrigger);
		}

		// Process for pillarboxing
		if (_cropPillar)
		{
			for (var x = 0; x < wMax; x += 2)
			{
				var avgLeft = GetColAverage(image, x, height);
				var avgRight = GetColAverage(image, width - x, height);
				if (avgLeft <= _cropBlackLevel && avgRight <= _cropBlackLevel && avgLeft == avgRight)
				{
					pPixels = x;
				}
				else
				{
					break;
				}
			}

			UpdateCrop(ref _pCrop, ref _pCropPixels, pPixels, _pFrameCropTrigger);
		}

		// Update sectors if needed
		if (_sectorChanged)
		{
			Log.Debug($"Crop changed, redrawing {_lCropPixels} and {_pCropPixels}...");
			_fullCoords = DrawGrid();
			_fullSectors = DrawSectors();
			_sectorChanged = false;
		}

		await Task.FromResult(true);
	}

// Helper method to calculate the average brightness of a row, avoiding direct raw data access
	private double GetRowAverage(Mat image, int rowIndex, int width)
	{
		var row = image.Row(rowIndex);
		var rowData = row.GetRawData().SkipLast(8).Skip(8).ToArray();
		var average = rowData.Average(byteValue => byteValue);
		row.Dispose();
		return average;
	}

// Helper method to calculate the average brightness of a column, similar to the row average calculation
	private double GetColAverage(Mat image, int colIndex, int height)
	{
		var col = image.Col(colIndex);
		var colData = col.GetRawData().SkipLast(8).Skip(8).ToArray();
		var average = colData.Average(byteValue => byteValue);
		col.Dispose();
		return average;
	}

// Method to update crop values and check if sector changed
	private void UpdateCrop(ref bool cropFlag, ref int cropPixels, int newPixels, FrameCropTrigger frameCropTrigger)
	{
		frameCropTrigger.Tick(newPixels);
		if (frameCropTrigger.Triggered != cropFlag && !_sectorChanged)
		{
			_sectorChanged = true;
			cropFlag = frameCropTrigger.Triggered;
		}
		cropPixels = newPixels;
	}

	
	private static int Sum(IEnumerable<byte> bytes) {
		return bytes.Aggregate(0, (current, b) => current + b);
	}

	private Rectangle[] DrawGrid() {
		var lOffset = _lCropPixels;
		var pOffset = _pCropPixels;
		var output = new Rectangle[_ledCount];

		// Bottom Region
		var bBottom = ScaleHeight - lOffset;
		var bTop = bBottom - _borderHeight;

		// Right Column Border
		var rRight = ScaleWidth - pOffset;
		var rLeft = rRight - _borderWidth;
		const float w = ScaleWidth;
		const float h = ScaleHeight;

		// Steps
		var widthTop = (int)Math.Ceiling(w / _topCount);
		var widthBottom = (int)Math.Ceiling(w / _bottomCount);
		var heightLeft = (int)Math.Ceiling(h / _leftCount);
		var heightRight = (int)Math.Ceiling(h / _rightCount);
		// Calc right regions, bottom to top
		var idx = 0;
		var pos = ScaleHeight - heightRight;

		for (var i = 0; i < _rightCount; i++) {
			if (pos < 0) {
				pos = 0;
			}

			output[idx] = new Rectangle((int)rLeft, pos, (int)_borderWidth, heightRight);
			pos -= heightRight;
			idx++;
		}

		// Calc top regions, from right to left
		pos = ScaleWidth - widthTop;

		for (var i = 0; i < _topCount; i++) {
			if (pos < 0) {
				pos = 0;
			}

			output[idx] = new Rectangle(pos, lOffset, widthTop, (int)_borderHeight);
			idx++;
			pos -= widthTop;
		}


		// Calc left regions (top to bottom)
		pos = 0;

		for (var i = 0; i < _leftCount; i++) {
			if (pos > ScaleHeight - heightLeft) {
				pos = ScaleHeight - heightLeft;
			}

			output[idx] = new Rectangle(pOffset, pos, (int)_borderWidth, heightLeft);
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

			output[idx] = new Rectangle(pos, (int)bTop, widthBottom, (int)_borderHeight);
			pos += widthBottom;
			idx++;
		}

		if (idx != _ledCount) {
			Log.Warning($"Warning: Led count is {idx - 1}, but should be {_ledCount}");
		}

		return output;
	}

	private Rectangle[] DrawCenterSectors() {
		var pOffset = _pCropPixels;
		var lOffset = _lCropPixels;
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
		var sectorWidth = (ScaleWidth - pOffset * 2) / _hSectors;
		var sectorHeight = (ScaleHeight - lOffset * 2) / _vSectors;
		// These are based on the border/strip values
		// Minimum limits for top, bottom, left, right            
		var top = ScaleHeight - lOffset - sectorHeight;
		var idx = 0;
		for (var v = _vSectors; v > 0; v--) {
			var left = ScaleWidth - pOffset - sectorWidth;
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
		var pOffset = _pCropPixels;
		var lOffset = _lCropPixels;
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
		var sectorWidth = (ScaleWidth - pOffset * 2) / _hSectors;
		var sectorHeight = (ScaleHeight - lOffset * 2) / _vSectors;
		// These are based on the border/strip values
		// Minimum limits for top, bottom, left, right            
		var minBot = ScaleHeight - lOffset - squareSize;
		// Calc right regions, bottom to top
		var idx = 0;
		var step = _vSectors - 1;
		while (step >= 0) {
			var size = step == _vSectors - 1 || step == 0 ? sectorWidth : squareSize;
			var x = ScaleWidth - pOffset - size;
			var ord = step * sectorHeight + lOffset;
			fs[idx] = new Rectangle(x, ord, size, sectorHeight);
			idx++;
			step--;
		}

		// Calc top regions, from right to left, skipping top-right corner (total horizontal sectors minus one)
		step = _hSectors - 2;
		while (step > 0) {
			var ord = step * sectorWidth + pOffset;
			fs[idx] = new Rectangle(ord, lOffset, sectorWidth, squareSize);
			idx++;
			step--;
		}

		step = 0;
		// Calc left regions (top to bottom), skipping top-left
		while (step <= _vSectors - 1) {
			var ord = step * sectorHeight + lOffset;
			var size = step == _vSectors - 1 || step == 0 ? sectorWidth : squareSize;
			fs[idx] = new Rectangle(pOffset, ord, size, sectorHeight);
			idx++;
			step++;
		}

		step = 1;
		// Calc bottom center regions (L-R)
		while (step <= _hSectors - 2) {
			var ord = step * sectorWidth + pOffset;
			fs[idx] = new Rectangle(ord, minBot, sectorWidth, squareSize);
			idx++;
			step += 1;
		}

		return fs;
	}
}