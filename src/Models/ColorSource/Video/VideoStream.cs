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
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Glimmr.Models.ColorSource.Video.Stream;
using Glimmr.Models.ColorSource.Video.Stream.Hdmi;
using Glimmr.Models.ColorSource.Video.Stream.PiCam;
using Glimmr.Models.ColorSource.Video.Stream.Screen;
using Glimmr.Models.ColorSource.Video.Stream.WebCam;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.Video {
	public sealed class VideoStream : IColorSource {
		
		public List<Color> Colors { get; private set; }
		public List<Color> Sectors { get; private set; }
		public bool SendColors { get; set; }
		
		// Scaling variables
		private const int ScaleHeight = 480;
		private const int ScaleWidth = 640;
		
		private Size _scaleSize;

		// Debug options
		private bool _noColumns;
		private bool _showEdged;
		private bool _showWarped;

		// Source stuff
		private PointF[] _vectors;
		private VectorOfPointF _lockTarget;

		private readonly List<VectorOfPoint> _targets;

		// Loaded data
		private int _camType;
		private int _captureMode;
		private int _srcArea;

		private SystemData _systemData;

		// Video source and splitter
		private readonly IVideoStream _vc;
		private Splitter StreamSplitter { get; set; }

		// Timer and bool for saving sample frames
		private Timer _saveTimer;
		private bool _doSave;
		public bool SourceActive;
		private ControlService _controlService;
		private readonly ColorService _colorService;


		public VideoStream(ColorService cs, ControlService controlService, CancellationToken camToken) {
			Log.Debug("Initializing stream capture...");
			_targets = new List<VectorOfPoint>();
			SetCapVars();
			_vc = GetStream();
			if (_vc == null) return;
			_vc.Start(camToken);
			_colorService = cs;
			_controlService = controlService;
			StreamSplitter = new Splitter(_systemData, _controlService);
			Log.Debug("Stream capture initialized.");
		}

		
		public void StartStream(CancellationToken ct) {
			Log.Debug("Initializing video stream...");
			SetCapVars();
			var autoEvent = new AutoResetEvent(false);
			_saveTimer = new Timer(SaveFrame, autoEvent, 5000, 5000);
			Log.Debug("Starting vid capture task...");
			while (!ct.IsCancellationRequested) {
				// Save cpu/memory by not doing anything if not sending...
				
				var frame = _vc.Frame;
				if (frame == null) {
					SourceActive = false;
					Log.Warning("Frame is null.");
					continue;
				}

				if (frame.Cols == 0) {
					SourceActive = false;
					if (!_noColumns) {
						Log.Warning("Frame has no columns.");
						_noColumns = true;
					}
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
				Colors = StreamSplitter.GetColors();
				Sectors = StreamSplitter.GetSectors();
				//Log.Debug("No, really, sending colors...");
				if (SendColors) _colorService.SendColors(this, new DynamicEventArgs(Colors, Sectors)).ConfigureAwait(true);
				
			}
			_saveTimer.Dispose();
			Log.Information("Capture task completed.");
		}

		public void StopStream() {
			
		}

		public void Refresh() {
			StreamSplitter?.Refresh();
		}


		private void SetCapVars() {
			_systemData = DataUtil.GetObject<SystemData>("SystemData");
			Colors = ColorUtil.EmptyList(_systemData.LedCount);
			Sectors = ColorUtil.EmptyList(28);
			_captureMode = DataUtil.GetItem<int>("CaptureMode");
			_camType = DataUtil.GetItem<int>("CamType");
			Log.Debug("Capture mode is " + _captureMode);
			_srcArea = ScaleWidth * ScaleHeight;
			_scaleSize = new Size(ScaleWidth, ScaleHeight);

			if (_captureMode == 1) {
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
				case 1:
					switch (_camType) {
						case 0:
							// 0 = pi module, 1 = web cam
							Log.Debug("Loading Pi cam.");
							var camMode = DataUtil.GetItem<int>("CamMode") ?? 1;
							return new PiCamVideoStream(ScaleWidth, ScaleHeight, camMode);
						case 1:
							Log.Debug("Loading web cam.");
							return new WebCamVideoStream(0);
					}

					return null;
				case 2:
					var cams = HdmiVideoStream.ListSources();
					return cams.Length != 0 ? new HdmiVideoStream(cams[0]) : null;
					
				case 3:
					Log.Debug("Loading screen capture.");
					return new ScreenVideoStream();
			}

			return null;
		}

		private void SaveFrame(object sender) {
			if (!_doSave) _doSave = true;
		}

		
		private Mat ProcessFrame(Mat input) {
			Mat output;
			// If we need to crop our image...do it.
			if (_captureMode == 1 && _camType != 2) // Crop our camera frame if the input is a camera
				output = CamFrame(input);
			// Otherwise, just return the input.
			else
				output = input;

			// Save a preview frame every 5 seconds
			if (_doSave) {
				_doSave = false;
				StreamSplitter.DoSave = true;
				var path = Directory.GetCurrentDirectory();
				input?.Save(path + "/wwwroot/img/_preview_input.jpg");
			}

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

		private VectorOfPointF CountTargets(List<VectorOfPoint> inputT) {
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