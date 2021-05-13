#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Glimmr.Enums;
using Glimmr.Models.ColorSource.Video.Stream;
using Glimmr.Models.ColorSource.Video.Stream.PiCam;
using Glimmr.Models.ColorSource.Video.Stream.Screen;
using Glimmr.Models.ColorSource.Video.Stream.Usb;
using Glimmr.Models.Util;
using Glimmr.Services;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.Video {
	public sealed class VideoStream : BackgroundService {
		
		public List<Color> Colors { get; private set; }
		public List<Color> Sectors { get; private set; }
		// should we send them to devices?
		public bool SendColors { get; set; }
		
		// Should we be processing?

		private bool _enable;
		
		// Scaling variables
		private const int ScaleHeight = DisplayUtil.CaptureHeight;
		private const int ScaleWidth = DisplayUtil.CaptureWidth;

		private Size _scaleSize;

		// Debug options
		private bool _noColumns;
		private bool _showEdged;
		private bool _showWarped;

		// Source stuff
		private PointF[] _vectors;
		private VectorOfPointF _lockTarget;

		private List<VectorOfPoint> _targets;

		// Loaded data
		private int _camType;
		private CaptureMode _captureMode;
		private int _srcArea;

		private SystemData _systemData;

		// Video source and splitter
		private IVideoStream _vc;
		private Splitter StreamSplitter { get; set; }

		// Timer and bool for saving sample frames
		private Timer _saveTimer;
		private bool _doSave;
		
		// Is content detected?
		public bool SourceActive;
		private readonly ColorService _colorService;
		private readonly ControlService _controlService;
		private CancellationToken _cancellationToken;

		private Stopwatch _frameWatch;


		public VideoStream(ColorService cs, ControlService controlService) {
			_frameWatch = new Stopwatch();
			_colorService = cs;
			_controlService = controlService;
			_controlService.RefreshSystemEvent += RefreshSystem;
			_colorService.AddStream("video", this);
		}

		private void RefreshSystem() {
			var wasEnabled = _enable;
			if (_enable) _enable = false;
			_vc?.Stop();
			var prevCapMode = _captureMode;
			SetCapVars();
			if (prevCapMode != _captureMode) {
				Log.Debug("Resetting capture mode?");
				_vc.Stop();
				_vc = GetStream();
				if (_vc == null) {
					Log.Warning("Video capture is null!");
					return;
				}
				_vc.Start(_cancellationToken);
			}
			StreamSplitter?.Refresh();
			Initialize(_cancellationToken);
			if (wasEnabled) _enable = true;
		}

		private void Initialize(CancellationToken ct) {
			_cancellationToken = ct;
			Log.Debug("Initializing video stream...");
			var autoEvent = new AutoResetEvent(false);
			_saveTimer = new Timer(SaveFrame, autoEvent, 0, 10000);
			_targets = new List<VectorOfPoint>();
			SetCapVars();
			_vc = GetStream();
			if (_vc == null) {
				Log.Warning("Video capture is null!");
				return;
			}
			_vc.Start(ct);
			StreamSplitter = new Splitter(_systemData, _controlService);
			Log.Debug("Stream capture initialized.");
		}

		public void ToggleStream(bool enable = false) {
			_enable = enable;
			SendColors = true;
		}

		
		protected override Task ExecuteAsync(CancellationToken ct) {
			Initialize(ct);
			Log.Debug("Starting video stream...");
			return Task.Run(async () => {
				while (!ct.IsCancellationRequested) {
					// if (!_frameWatch.IsRunning) _frameWatch.Start();
					// if (_frameWatch.Elapsed < TimeSpan.FromMilliseconds(16.6666666)) {
					// 	Log.Debug("Frame watch: " + _frameWatch.ElapsedMilliseconds);
					// 	return;
					// } else {
					// 	Log.Debug("Resetting watch...");
					// }
					// _frameWatch.Restart();
					// Save cpu/memory by not doing anything if not sending...
					if (!_enable) {
						await Task.Delay(1, ct);
						continue;
					}
				
					var frame = _vc.Frame;
					if (frame == null) {
						SourceActive = false;
						//Log.Warning("Frame is null.");
						continue;
					}

					if (frame.Cols == 0) {
						SourceActive = false;
						if (!_noColumns) {
							Log.Warning("Frame has no columns.");
							_noColumns = true;
						}

						//Log.Debug("NO COLUMNS");
						continue;
					}
					
					
					var warped = ProcessFrame(frame);
					if (warped == null) {
						SourceActive = false;
						Log.Warning("Unable to process frame.");
						continue;
					}

					StreamSplitter.Update(warped);
					SourceActive = !StreamSplitter.NoImage;
					var c1 = StreamSplitter.GetColors();
					Colors = c1;
					var c2 = StreamSplitter.GetSectors();
					Sectors = c2;
					if (SendColors) {
						_colorService.SendColors(Colors, Sectors, 0);
					}
				}
				await _saveTimer.DisposeAsync();
				Log.Information("Capture task completed.");
			}, CancellationToken.None);
		}

		
		public void Refresh() {
			StreamSplitter?.Refresh();
		}


		private void SetCapVars() {
			_systemData = DataUtil.GetSystemData();
			Colors = ColorUtil.EmptyList(_systemData.LedCount);
			var sectorSize = (_systemData.VSectors * 2) + (_systemData.HSectors * 2) - 4; 
			Sectors = ColorUtil.EmptyList(sectorSize);
			_captureMode = (CaptureMode) DataUtil.GetItem<int>("CaptureMode");
			_camType = DataUtil.GetItem<int>("CamType");
			Log.Debug("Capture mode is " + _captureMode);
			_srcArea = ScaleWidth * ScaleHeight;
			_scaleSize = new Size(ScaleWidth, ScaleHeight);

			if (_captureMode == CaptureMode.Camera) {
				try {
					var lt = DataUtil.GetItem<PointF[]>("LockTarget");
					Log.Debug("LT Grabbed? " + JsonConvert.SerializeObject(lt));
					if (lt != null) {
						_lockTarget = new VectorOfPointF(lt);
						var lC = 0;
						while (lC < 20) {
							_targets.Add(VPointFToVPoint(_lockTarget));
							lC++;
						}
					}
				} catch (Exception e) {
					Log.Debug("Exception: " + e.Message);
				}

			}
			_vectors = new PointF[] {new Point(0, 0), new Point(ScaleWidth, 0), new Point(ScaleWidth, ScaleHeight), new Point(0, ScaleHeight)};

			// Debugging vars...
			_showEdged = DataUtil.GetItem<bool>("ShowEdged") ?? false;
			_showWarped = DataUtil.GetItem<bool>("ShowWarped") ?? false;
			Log.Debug("Start Capture should be running...");
		}

		private IVideoStream GetStream() {
			switch (_captureMode) {
				case CaptureMode.Camera:
					switch (_camType) {
						case 0:
							// 0 = pi module, 1 = web cam
							Log.Debug("Loading Pi cam.");
							return new PiCamVideoStream();
						case 1:
							Log.Debug("Loading web cam.");
							return new UsbVideoStream();
					}

					return null;
				case CaptureMode.Hdmi:
					return new UsbVideoStream();
					
				case CaptureMode.Screen:
					Log.Debug("Loading screen capture.");
					return new ScreenVideoStream();
			}

			return null;
		}

		private void SaveFrame(object sender) {
			if (!_doSave) {
				_doSave = true;
				_vc?.SaveFrame();
			}
		}

		
		private Mat ProcessFrame(Mat input) {
			Mat output;
			// If we need to crop our image...do it.
			if (_captureMode == CaptureMode.Camera && _camType != 2) // Crop our camera frame if the input is a camera
				output = CamFrame(input);
			// Otherwise, just return the input.
			else
				output = input;

			// Save a preview frame every 5 seconds
			if (!_doSave) {
				return output;
			}

			_doSave = false;
			StreamSplitter.DoSave = true;
			var path = Directory.GetCurrentDirectory();
			var fullPath = Path.Join(path, "wwwroot", "img", "_preview_input.jpg");
			input?.Save(fullPath);

			return output;
		}


		private Mat CamFrame(Mat input) {
			Mat output = null;
			var scaled = input.Clone();

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
				if (_showWarped) CvInvoke.Imshow("Warped", warpMat);
				warpMat.Dispose();
			}

			scaled.Dispose();
			// Once we have a warped frame, we need to do a check every N seconds for letterboxing...
			return output;
		}

		private VectorOfPointF FindTarget(Mat input) {
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
					if (approxContour.Size != 4) continue;
					var cntArea = CvInvoke.ContourArea(approxContour);
					if (!(cntArea / _srcArea > .15)) continue;
					var pointOut = new VectorOfPointF(SortPoints(approxContour));
					_targets.Add(VPointFToVPoint(pointOut));
				}

				if (_showEdged) {
					var color = new MCvScalar(255, 255, 0);
					CvInvoke.DrawContours(input, contours, -1, color);
					CvInvoke.Imshow("Edged", cannyEdges);
				}
			}

			var output = CountTargets(_targets);
			cannyEdges.Dispose();
			return output;
		}

		private VectorOfPointF CountTargets(IReadOnlyCollection<VectorOfPoint> inputT) {
			VectorOfPointF output = null;
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

				PointF[] avgPoints = {new PointF(x1, y1), new PointF(x2, y2), new PointF(x3, y3), new PointF(x4, y4)};
				var avgVector = new VectorOfPointF(avgPoints);
				if (iCount > 20) output = avgVector;
			}

			if (iCount > 200) _targets.RemoveRange(0, 150);

			return output;
		}

		private static VectorOfPoint VPointFToVPoint(VectorOfPointF input) {
			var ta = input.ToArray();
			var pIn = new Point[input.Size];
			for (var i = 0; i < ta.Length; i++) pIn[i] = new Point((int) ta[i].X, (int) ta[i].Y);
			return new VectorOfPoint(pIn);
		}


		private static PointF[] SortPoints(VectorOfPoint wTarget) {
			var ta = wTarget.ToArray();
			var pIn = new PointF[wTarget.Size];
			for (var i = 0; i < ta.Length; i++) pIn[i] = ta[i];
			// Order points?
			var tPoints = pIn.OrderBy(p => p.Y);
			var vPoints = pIn.OrderByDescending(p => p.Y);
			var vtPoints = tPoints.Take(2);
			var vvPoints = vPoints.Take(2);
			vtPoints = vtPoints.OrderBy(p => p.X);
			vvPoints = vvPoints.OrderByDescending(p => p.X);
			var tl = vtPoints.ElementAt(0);
			var tr = vtPoints.ElementAt(1);
			var br = vvPoints.ElementAt(0);
			var bl = vvPoints.ElementAt(1);
			PointF[] outPut = {tl, tr, br, bl};
			return outPut;
		}

		
	}
}