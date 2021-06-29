#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using Glimmr.Enums;
using Glimmr.Models.ColorSource.Video.Stream;
using Glimmr.Models.ColorSource.Video.Stream.PiCam;
using Glimmr.Models.ColorSource.Video.Stream.Screen;
using Glimmr.Models.ColorSource.Video.Stream.Usb;
using Glimmr.Models.Util;
using Glimmr.Services;
using Microsoft.Extensions.Hosting;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.Video {
	public sealed class VideoStream : BackgroundService, IColorSource {
		// should we send them to devices?
		public bool SendColors { get; set; }

		public List<Color> Colors { get; private set; }
		public List<Color> Sectors { get; private set; }
		private Splitter? StreamSplitter { get; }

		// Scaling variables
		private readonly int _scaleHeight = DisplayUtil.CaptureHeight();
		private readonly int _scaleWidth = DisplayUtil.CaptureWidth();

		// Is content detected?
		private readonly ColorService _colorService;

		// Loaded data
		private int _camType;
		private CancellationToken _cancellationToken;
		private CaptureMode _captureMode;
		private bool _doSave;

		// Should we be processing?

		private bool _enable;
		private bool _wasEnabled;

		private VectorOfPointF? _lockTarget;

		// Debug options
		private bool _noColumns;

		// Timer and bool for saving sample frames
		private readonly Timer _saveTimer;

		private Size _scaleSize;
		private int _srcArea;

		private SystemData _systemData;

		private readonly List<VectorOfPoint> _targets;

		// Video source and splitter
		private IVideoStream? _vc;

		// Source stuff
		private PointF[] _vectors;


		public VideoStream(ControlService controlService) {
			Colors = new List<Color>();
			Sectors = new List<Color>();
			_vectors = Array.Empty<PointF>();
			_systemData = DataUtil.GetSystemData();
			_colorService = controlService.ColorService;
			ControlService controlService1 = controlService;
			controlService1.RefreshSystemEvent += RefreshSystem;
			_colorService.AddStream(DeviceMode.Video, this);
			var autoEvent = new AutoResetEvent(false);
			_saveTimer = new Timer(SaveFrame, autoEvent, 0, 10000);
			_targets = new List<VectorOfPoint>();
			StreamSplitter = new Splitter(controlService1);
		}

		public void ToggleStream(bool enable = false) {
			_enable = enable;
			if (!enable) _wasEnabled = true;
			SendColors = true;
		}


		public void Refresh(SystemData systemData) {
			StreamSplitter?.Refresh();
		}

		public bool SourceActive { get; set; }

		private void RefreshSystem() {
			var prevSd = _systemData;
			_systemData = DataUtil.GetSystemData();

			var wasEnabled = _enable;
			if (_enable) {
				_enable = false;
			}

			var prevCapMode = _captureMode;
			_captureMode = (CaptureMode) _systemData.CaptureMode;
			if (prevCapMode != _captureMode) {
				_vc?.Stop();
				_vc = GetStream();
				if (_vc == null) {
					Log.Information("We have no video source.");
					return;
				}

				_vc.Start(_cancellationToken);
			}

			var mode = (DeviceMode) _systemData.DeviceMode;
			var prevMode = (DeviceMode) prevSd.DeviceMode;
			if (mode == DeviceMode.Video && prevMode == DeviceMode.Video) {
				if (_systemData.SectorCount != prevSd.SectorCount || _systemData.LedCount != prevSd.LedCount) {
					StreamSplitter?.Refresh();
					Initialize(_cancellationToken);
				}

				if (wasEnabled) {
					_enable = true;
				}
			}
		}

		private void Initialize(CancellationToken ct) {
			_cancellationToken = ct;
			SetCapVars();
			_vc = GetStream();
			if (_vc == null) {
				Log.Information("We have no video source, returning.");
				return;
			}
			_vc.Start(ct);
			SendColors = true;
		}


		protected override Task ExecuteAsync(CancellationToken ct) {
			Log.Debug("Starting video stream service...");
			Initialize(ct);
			return Task.Run(async () => {
				while (!ct.IsCancellationRequested) {
					if (!_enable) {
						await Task.Delay(1, ct);
						continue;
					}

					try {
						//if (_enable && !_wasEnabled) Log.Debug("Starting frame?");

						if (_vc == null) continue;
						var frame = _vc.Frame;
						if (_enable && !_wasEnabled) Log.Debug("Got frame?");
						if (frame == null || frame.IsEmpty) {
							SourceActive = false;
							Log.Warning("Frame is null.");
							continue;
						}

						if (_enable && !_wasEnabled) Log.Debug("Col check...");
						if (frame.Cols == 0) {
							SourceActive = false;
							if (!_noColumns) {
								Log.Warning("Frame has no columns.");
								_noColumns = true;
							}

							//Log.Debug("NO COLUMNS");
							continue;
						}

						if (_enable && !_wasEnabled) Log.Debug("Processing frame?");
						var warped = ProcessFrame(frame);
						if (warped == null) {
							SourceActive = false;
							Log.Warning("Unable to process frame.");
							continue;
						}

						if (_enable && !_wasEnabled) Log.Debug("Updating splitter?");
						StreamSplitter?.Update(warped);
						if (StreamSplitter == null) return;
						SourceActive = !StreamSplitter.NoImage;
						var c1 = StreamSplitter.GetColors();
						Colors = c1;
						var c2 = StreamSplitter.GetSectors();
						Sectors = c2;
						if (_enable && !_wasEnabled) Log.Debug("Sending frame?");
						if (SendColors) {
							_colorService.SendColors(Colors, Sectors, 0);
						}
					} catch (Exception e) {
						Log.Warning("Exception: " + e.Message);
					}

					_wasEnabled = _enable;
				}

				await _saveTimer.DisposeAsync();
				Log.Information("Video stream service stopped.");
			}, CancellationToken.None);
		}


		private void SetCapVars() {
			_systemData = DataUtil.GetSystemData();
			Colors = ColorUtil.EmptyList(_systemData.LedCount);
			var sectorSize = _systemData.VSectors * 2 + _systemData.HSectors * 2 - 4;
			Sectors = ColorUtil.EmptyList(sectorSize);
			_captureMode = (CaptureMode) _systemData.CaptureMode;
			_camType = DataUtil.GetItem<int>("CamType");
			_srcArea = _scaleWidth * _scaleHeight;
			_scaleSize = new Size(_scaleWidth, _scaleHeight);

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
					Log.Debug("Exception: " + e.Message);
				}
			}

			_vectors = new PointF[] {
				new Point(0, 0), new Point(_scaleWidth, 0), new Point(_scaleWidth, _scaleHeight), new Point(0, _scaleHeight)
			};
		}

		private IVideoStream? GetStream() {
			switch (_captureMode) {
				case CaptureMode.Camera:
					switch (_camType) {
						case 0:
							// 0 = pi module, 1 = web cam
							Log.Debug("Using Pi cam for capture.");
							return new PiCamVideoStream();
						case 1:
							Log.Debug("Using web cam for capture.");
							return new UsbVideoStream();
					}

					return null;
				case CaptureMode.Hdmi:
					Log.Debug("Using usb stream for capture.");
					return new UsbVideoStream();

				case CaptureMode.Screen:
					Log.Debug("Using screen for capture.");
					return new ScreenVideoStream();
			}

			return null;
		}

		private void SaveFrame(object? sender) {
			if (_doSave) {
				return;
			}

			_doSave = true;
			_vc?.SaveFrame();
		}


		private Mat? ProcessFrame(Mat? input) {
			if (input == null) return input;
			Mat? output;
			// If we need to crop our image...do it.
			if (_captureMode == CaptureMode.Camera && _camType != 2) // Crop our camera frame if the input is a camera
			{
				output = CamFrame(input);
			}
			// Otherwise, just return the input.
			else {
				output = input;
			}

			// Save a preview frame every 5 seconds
			if (!_doSave) {
				return output;
			}

			_doSave = false;
			if (StreamSplitter != null) StreamSplitter.DoSave = true;
			var path = Directory.GetCurrentDirectory();
			var fullPath = Path.Join(path, "wwwroot", "img", "_preview_input.jpg");
			input.Save(fullPath);

			return output;
		}


		private Mat? CamFrame(Mat input) {
			Mat? output = null;
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
	}
}