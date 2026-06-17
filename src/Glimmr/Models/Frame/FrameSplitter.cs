#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Timers;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.IntensityTransform;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Glimmr.Enums;
using Glimmr.Models.ColorTarget.Wled;
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

	/// <summary>
	///     True while a frame is being processed. Capture callbacks can check this to skip
	///     filling buffers for frames that would be dropped anyway.
	/// </summary>
	public bool IsBusy => _updating;

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
	private Color[] _colorsMatrix = Array.Empty<Color>();
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
	private Color[] _emptyMatrix = Array.Empty<Color>();

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

	// Reusable processing buffers - allocated once and reused every frame to keep
	// the hot path allocation-free.
	private readonly Mat _resizeMat = new();
	private readonly Mat _gammaMat = new();
	private readonly Mat _warpMat = new();
	private Mat? _warpTransform;

	// Cached ROI views into the processed frame. Rebuilt only when the source mat or
	// the grid geometry changes, instead of allocating hundreds of Mats per frame.
	private Mat[] _ledRois = Array.Empty<Mat>();
	private Mat[] _sectorRois = Array.Empty<Mat>();
	private Mat? _roiSource;
	private IntPtr _roiSourcePtr;
	private bool _roiDirty = true;

	// Double-buffered output: colors are written into the inactive buffer, then the
	// published reference is swapped so concurrent readers (e.g. AudioVideoStream)
	// always see a fully written array.
	private readonly Color[][] _ledBuffers = { Array.Empty<Color>(), Array.Empty<Color>() };
	private readonly Color[][] _sectorBuffers = { Array.Empty<Color>(), Array.Empty<Color>() };
	private readonly Color[][] _matrixBuffers = { Array.Empty<Color>(), Array.Empty<Color>() };
	private int _bufferIndex;

	// True while a frame is in flight; overlapping updates are dropped so processing
	// never builds a backlog and always works on the freshest frame.
	private volatile bool _updating;

	// Set this when the sector changes
	private bool _sectorChanged;
	private int _sectorCount;
	// Upper bound for the adaptive content-grid resolution so a huge configured panel
	// can't explode the per-frame ROI count.
	private const int MaxMatrixSample = 64;
	private int _matrixSampleWidth;
	private int _matrixSampleHeight;
	// 2D matrix content grid: a single INTER_AREA downscale of the frame (one native call)
	// instead of per-cell Mean. Only computed when an enabled matrix device exists.
	private bool _matrixActive;
	private Mat? _matrixSmall;
	private byte[] _matrixBytes = Array.Empty<byte>();
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
		_resizeMat.Dispose();
		_gammaMat.Dispose();
		_warpMat.Dispose();
		_warpTransform?.Dispose();
		_matrixSmall?.Dispose();
		ClearRoiCache();
		GC.SuppressFinalize(this);
	}

	private void ClearRoiCache() {
		foreach (var roi in _ledRois) {
			roi.Dispose();
		}

		foreach (var roi in _sectorRois) {
			roi.Dispose();
		}

		_ledRois = Array.Empty<Mat>();
		_sectorRois = Array.Empty<Mat>();
		_roiSource = null;
		_roiSourcePtr = IntPtr.Zero;
		_roiDirty = true;
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

		var matrixW = sd.MatrixSampleWidth < 1 ? 24 : sd.MatrixSampleWidth;
		var matrixH = sd.MatrixSampleHeight < 1 ? 32 : sd.MatrixSampleHeight;

		// Adapt the content-grid resolution up to the largest enabled 2D matrix panel so
		// Mirror / high-detail modes aren't bottlenecked by the default sample grid. The
		// configured SystemData values act as a floor; the grid resolution is capped. We also
		// only compute the matrix grid at all when at least one matrix device is enabled.
		_matrixActive = false;
		foreach (var wled in DataUtil.GetDevices<WledData>("Wled")) {
			if (wled is not { Enable: true, StripMode: StripMode.Matrix }) {
				continue;
			}

			_matrixActive = true;
			if (wled.MatrixWidth > matrixW) {
				matrixW = wled.MatrixWidth;
			}

			if (wled.MatrixHeight > matrixH) {
				matrixH = wled.MatrixHeight;
			}
		}

		_matrixSampleWidth = Math.Clamp(matrixW, 1, MaxMatrixSample);
		_matrixSampleHeight = Math.Clamp(matrixH, 1, MaxMatrixSample);

		_emptyMatrix = ColorUtil.EmptyColors(_matrixSampleWidth * _matrixSampleHeight);
		_matrixSmall?.Dispose();
		_matrixSmall = new Mat(_matrixSampleHeight, _matrixSampleWidth, DepthType.Cv8U, 3);
		_matrixBytes = new byte[_matrixSampleWidth * _matrixSampleHeight * 3];

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

		// Lock target (or vectors) may have changed; recompute the warp transform lazily.
		_warpTransform?.Dispose();
		_warpTransform = null;

		_fullCoords = DrawGrid();
		_fullSectors = DrawSectors();
		_roiDirty = true;
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

	/// <summary>
	///     Process a captured (or synthetic) frame into LED and sector colors.
	///     The caller retains ownership of <paramref name="frame" /> and is responsible for
	///     disposing it; the splitter never disposes input frames.
	///     If a frame is already being processed, the new frame is dropped immediately so
	///     processing never builds a backlog - output always reflects the freshest frame.
	/// </summary>
	public async Task<(Color[], Color[])> Update(Mat? frame)
	{
		if (frame == null || frame.IsEmpty || frame.Cols == 0 || frame.Rows == 0)
		{
			SourceActive = false;
			LogWarningOnce("Frame is null or empty.");
			return (Array.Empty<Color>(), Array.Empty<Color>());
		}

		if (_updating)
		{
			// Drop the stale frame; return the most recent published colors.
			return (_colorsLed, _colorsSectors);
		}

		_updating = true;
		try
		{
			var processedFrame = ProcessFrame(frame);

			if (processedFrame == null || processedFrame.IsEmpty)
			{
				LogWarningOnce("Processed frame is null or empty.");
				return (Array.Empty<Color>(), Array.Empty<Color>());
			}

			if (_useCrop && _checkCrop)
			{
				await CheckCrop(processedFrame).ConfigureAwait(false);
				_checkCrop = false;
			}

			var (ledColors, sectorColors, matrixColors) = ComputeColors(processedFrame);

			_colorsLed = ledColors;
			_colorsSectors = sectorColors;
			_colorsMatrix = matrixColors;

			if (_doSend)
			{
				await ColorService.SendColors(ledColors, sectorColors, matrixColors,
					_matrixSampleWidth, _matrixSampleHeight);
			}

			await HandleSaving(frame, processedFrame).ConfigureAwait(false);

			return (_colorsLed, _colorsSectors);
		}
		finally
		{
			_updating = false;
		}
	}

	private void LogWarningOnce(string message)
	{
		if (!_warned)
		{
			Log.Warning(message);
			_warned = true;
		}
	}

	private Mat? ProcessFrame(Mat frame)
	{
		var output = frame;
		if (ShouldResize(output))
		{
			// Resize into the persistent buffer; OpenCV reuses the allocation when the size matches.
			CvInvoke.Resize(output, _resizeMat, _scaleSize);
			output = _resizeMat;
		}

		if (_captureMode == CaptureMode.Camera && _mode == DeviceMode.Video)
		{
			output = CheckCamera(output);
			if (output == null)
			{
				return null;
			}
		}

		if (_correctGamma)
		{
			IntensityTransformInvoke.GammaCorrection(output, _gammaMat, _gammaCorrection);
			output = _gammaMat;
		}

		return output;
	}

	private static bool ShouldResize(Mat frame)
	{
		return frame.Width != ScaleWidth || frame.Height != ScaleHeight;
	}

	/// <summary>
	///     Rebuilds the cached ROI views when the source mat or grid geometry changes.
	///     For the video path the processed frame lives in a persistent buffer, so this is
	///     effectively a one-time cost instead of hundreds of Mat allocations per frame.
	/// </summary>
	private void EnsureRoiCache(Mat frame)
	{
		if (!_roiDirty && ReferenceEquals(_roiSource, frame) && frame.DataPointer == _roiSourcePtr)
		{
			return;
		}

		ClearRoiCache();
		var ledRois = new Mat[_fullCoords.Length];
		for (var i = 0; i < _fullCoords.Length; i++)
		{
			ledRois[i] = new Mat(frame, _fullCoords[i]);
		}

		var sectorRois = new Mat[_fullSectors.Length];
		for (var i = 0; i < _fullSectors.Length; i++)
		{
			sectorRois[i] = new Mat(frame, _fullSectors[i]);
		}

		_ledRois = ledRois;
		_sectorRois = sectorRois;
		_roiSource = frame;
		_roiSourcePtr = frame.DataPointer;
		_roiDirty = false;
	}

	private (Color[], Color[], Color[]) ComputeColors(Mat frame)
	{
		if (_allBlack) return (_empty, _emptySectors, _emptyMatrix);

		EnsureRoiCache(frame);

		// Write into the inactive buffers, then publish - readers never see a half-written array.
		var idx = _bufferIndex ^ 1;
		var ledColors = _ledBuffers[idx];
		if (ledColors.Length != _ledRois.Length)
		{
			ledColors = new Color[_ledRois.Length];
			_ledBuffers[idx] = ledColors;
		}

		var sectorColors = _sectorBuffers[idx];
		if (sectorColors.Length != _sectorRois.Length)
		{
			sectorColors = new Color[_sectorRois.Length];
			_sectorBuffers[idx] = sectorColors;
		}

		var matrixLen = _matrixActive ? _matrixSampleWidth * _matrixSampleHeight : 0;
		var matrixColors = _matrixBuffers[idx];
		if (matrixColors.Length != matrixLen)
		{
			matrixColors = new Color[matrixLen];
			_matrixBuffers[idx] = matrixColors;
		}

		var ledRois = _ledRois;
		Parallel.For(0, ledRois.Length, i => { ledColors[i] = GetAverage(ledRois[i]); });

		var sectorRois = _sectorRois;
		Parallel.For(0, sectorRois.Length, i => { sectorColors[i] = GetAverage(sectorRois[i]); });

		if (_matrixActive)
		{
			ComputeMatrixColors(frame, matrixColors);
		}

		_bufferIndex = idx;
		return (ledColors, sectorColors, matrixColors);
	}

	/// <summary>
	///     Build the 2D matrix content grid with a single area-averaging downscale (one native
	///     call) instead of a Mean per cell. Output is row-major, top-left, BGR-&gt;RGB, with the
	///     same near-black clamp the per-cell path used.
	/// </summary>
	private void ComputeMatrixColors(Mat frame, Color[] matrixColors)
	{
		if (_matrixSmall == null || matrixColors.Length == 0)
		{
			return;
		}

		CvInvoke.Resize(frame, _matrixSmall, new Size(_matrixSampleWidth, _matrixSampleHeight), 0, 0, Inter.Area);

		var len = Math.Min(_matrixBytes.Length,
			_matrixSmall.Rows * _matrixSmall.Cols * _matrixSmall.NumberOfChannels);
		Marshal.Copy(_matrixSmall.DataPointer, _matrixBytes, 0, len);

		for (var i = 0; i < matrixColors.Length; i++)
		{
			var j = i * 3;
			int b = _matrixBytes[j];
			int g = _matrixBytes[j + 1];
			int r = _matrixBytes[j + 2];
			matrixColors[i] = r < _blackLevel && g < _blackLevel && b < _blackLevel
				? Color.FromArgb(0, 0, 0, 0)
				: Color.FromArgb(r, g, b);
		}
	}

	private async Task HandleSaving(Mat original, Mat processed)
	{
		if (!_doSave || (!_doSend && !_merge) || !ColorService.ControlService.SendPreview)
		{
			return;
		}

		_doSave = false;
		var merge = _merge;
		_merge = false;
		await SaveFrames(original, processed, merge).ConfigureAwait(false);
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

	
	private async Task SaveFrames(Mat inMat, Mat outMat, bool merge) {
		var cols = _colorsLed;
		var secs = _colorsSectors;
		if (merge) {
			cols = _colorsLedIn;
			secs = _colorsSectorsIn;
		}

		// Work on clones: the source mats are persistent buffers that will be overwritten
		// by the next frame, and the preview encode/send is async.
		if (inMat is { IsEmpty: false }) {
			using var inClone = inMat.Clone();
			await ControlService.SendImage("inputImage", inClone).ConfigureAwait(false);
		}

		if (outMat.IsEmpty) {
			Log.Debug("Output image is empty.");
			return;
		}

		using var outClone = outMat.Clone();
		var colBlack = new Bgr(Color.FromArgb(255, 128, 128, 128)).MCvScalar;
		switch (_previewMode) {
			
			case 1: {
				for (var i = 0; i < _fullCoords.Length && i < cols.Length; i++) {
					var color = cols[i];
					if (color.R < _blackLevel && color.G < _blackLevel && color.B < _blackLevel) {
						color = Color.Black;
					}

					var col = new Bgr(color).MCvScalar;
					CvInvoke.Rectangle(outClone, _fullCoords[i], col, -1, LineType.AntiAlias);
					CvInvoke.Rectangle(outClone, _fullCoords[i], colBlack, 1, LineType.AntiAlias);
				}
				break;
			}
			case 2: {
				for (var i = 0; i < _fullSectors.Length && i < secs.Length; i++) {
					var s = _fullSectors[i];
					var color = secs[i];
					if (color.R < _blackLevel && color.G < _blackLevel && color.B < _blackLevel) {
						color = Color.FromArgb(0, 0, 0);
					}

					var col = new Bgr(color).MCvScalar;
					CvInvoke.Rectangle(outClone, s, col, -1, LineType.AntiAlias);
					CvInvoke.Rectangle(outClone, s, colBlack, 1, LineType.AntiAlias);
					var cInt = i + 1;
					var tPoint = new Point(s.X, s.Y + 30);
					CvInvoke.PutText(outClone, cInt.ToString(), tPoint, FontFace.HersheySimplex, 0.75, colBlack);
				}
				break;
			}
		}

		if (outClone is { IsEmpty: false }) {
			await ControlService.SendImage("outputImage", outClone).ConfigureAwait(false);
		}
	}


	private Mat? CheckCamera(Mat input) {
		// If we don't have a target, find one. FindTarget only reads the input frame.
		if (_lockTarget == null) {
			_lockTarget = FindTarget(input);
			if (_lockTarget != null) {
				Log.Debug("Target hit.");
				DataUtil.SetItem("LockTarget", _lockTarget.ToArray());
				_warpTransform?.Dispose();
				_warpTransform = null;
			} else {
				Log.Debug("No target.");
			}
		}

		if (_lockTarget == null) {
			return null;
		}

		// The perspective transform only depends on the lock target, so compute it once
		// and reuse it; only the warp itself runs per frame, into a persistent buffer.
		_warpTransform ??= CvInvoke.GetPerspectiveTransform(_lockTarget.ToArray(), _vectors);
		CvInvoke.WarpPerspective(input, _warpMat, _warpTransform, _scaleSize);
		return _warpMat;
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

	/// <summary>
	///     Most recent full-frame matrix content sample (row-major, top-left origin).
	///     <see cref="MatrixWidth" /> columns by <see cref="MatrixHeight" /> rows.
	/// </summary>
	public Color[] GetMatrix() {
		return _colorsMatrix;
	}

	public int MatrixWidth => _matrixSampleWidth;
	public int MatrixHeight => _matrixSampleHeight;
	
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

		// Detect a dark frame from the whole-frame average brightness (more robust than sampling
		// a handful of rows, which could miss/over-weight partial content). Drives auto-disable.
		var mean = CvInvoke.Mean(image);
		var avgBrightness = (mean.V0 + mean.V1 + mean.V2) / 3.0;
		var sampleBlack = avgBrightness <= _cropBlackLevel;

		_allBlack = sampleBlack;
		if (_doSend)
		{
			ColorService.CheckAutoDisable(!sampleBlack);
		}

		if (!_useCrop || sampleBlack)
		{
			return;
		}

		var lPixels = 0;
		var pPixels = 0;

		// Process for letterboxing
		if (_cropLetter)
		{
			for (var y = 0; y < hMax; y += 2)
			{
				var avgTop = GetRowAverage(image, y);
				var avgBottom = GetRowAverage(image, height - y);
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
				var avgLeft = GetColAverage(image, x);
				var avgRight = GetColAverage(image, width - x);
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
			_roiDirty = true;
			_sectorChanged = false;
		}

		await Task.FromResult(true);
	}

	/// <summary>
	///     Average byte value of a row, reading the mat data directly - no per-call
	///     allocations or LINQ. Skips the first/last 8 bytes like the legacy implementation.
	/// </summary>
	private static unsafe int GetRowAverage(Mat image, int rowIndex)
	{
		var bytesPerRow = image.Cols * image.NumberOfChannels;
		if (bytesPerRow <= 16)
		{
			return 0;
		}

		var ptr = (byte*)image.DataPointer + (long)rowIndex * image.Step;
		long sum = 0;
		for (var i = 8; i < bytesPerRow - 8; i++)
		{
			sum += ptr[i];
		}

		return (int)(sum / (bytesPerRow - 16));
	}

	/// <summary>
	///     Average byte value of a column, reading the mat data directly.
	///     Skips ~8 bytes (3 pixels) at the top and bottom edges.
	/// </summary>
	private static unsafe int GetColAverage(Mat image, int colIndex)
	{
		var channels = image.NumberOfChannels;
		var rows = image.Rows;
		const int skip = 3;
		if (rows <= skip * 2)
		{
			return 0;
		}

		var step = image.Step;
		var basePtr = (byte*)image.DataPointer + colIndex * channels;
		long sum = 0;
		for (var r = skip; r < rows - skip; r++)
		{
			var p = basePtr + (long)r * step;
			for (var c = 0; c < channels; c++)
			{
				sum += p[c];
			}
		}

		return (int)(sum / ((rows - skip * 2) * channels));
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