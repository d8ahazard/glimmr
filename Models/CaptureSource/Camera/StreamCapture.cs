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
using HueDream.Models.CaptureSource.HDMI;
using HueDream.Models.CaptureSource.ScreenCapture;
using HueDream.Models.DreamScreen;
using HueDream.Models.LED;
using HueDream.Models.Util;
using Newtonsoft.Json;

namespace HueDream.Models.CaptureSource.Camera {
    public sealed class StreamCapture {
        private int _scaleHeight = 400;
        private int _scaleWidth = 600;
        private float _scaleFactor = 0.5f;
        private int _camWidth;
        private int _camHeight;
        private bool _showSource;
        private bool _showEdged;
        private bool _showWarped;
        private PointF[] _vectors;
        private int _camType;
        private int _captureMode;
        private int _srcArea;
        private Size _scaleSize;
        private LedData _ledData;
        private int _frameCount;
        private DreamClient _dc;
        private VectorOfPointF _lockTarget;
        private List<VectorOfPoint> _targets;
        private IVideoStream _vc;
        private Splitter _splitter;
        private Mat _k;
        private Mat _d;
        private Task _capTask;


        public StreamCapture(CancellationToken camToken) {
            LogUtil.WriteInc("Initializing stream capture.");
            _targets = new List<VectorOfPoint>();
            SetCapVars();
            _vc = GetCamera();
            _vc.Start(camToken);
        }

        private void SetCapVars() {
            _ledData = DataUtil.GetItem<LedData>("ledData");
            _captureMode = DataUtil.GetItem<int>("captureMode");
            _camType = DataUtil.GetItem<int>("camType");

            if (_captureMode == 1) {

                _camWidth = DataUtil.GetItem<int>("camWidth") ?? 1920;
                _camHeight = DataUtil.GetItem<int>("camHeight") ?? 1080;
                _scaleFactor = DataUtil.GetItem<float>("scaleFactor") ?? .5f;
                _scaleWidth = Convert.ToInt32(_camWidth * _scaleFactor);
                _scaleHeight = Convert.ToInt32(_camHeight * _scaleFactor);
                _srcArea = _scaleWidth * _scaleHeight;
                _scaleSize = new Size(_scaleWidth, _scaleHeight);

                if (_camType == 0) {
                    var kStr = DataUtil.GetItem("k");
                    if (kStr == null) {
                        LogUtil.Write("Running static camera calibration.");
                        Calibrate.ProcessFrames();
                    } else {
                        LogUtil.Write("Camera calibration settings loaded.");
                    }

                    kStr = DataUtil.GetItem("k");
                    var dStr = DataUtil.GetItem("d");
                    _k = JsonConvert.DeserializeObject<Mat>(kStr);
                    _d = JsonConvert.DeserializeObject<Mat>(dStr);
                    LogUtil.Write("calibration vars deserialized.");
                }

                try {
                    var lt = DataUtil.GetItem<PointF[]>("lockTarget");
                    LogUtil.Write("LT Grabbed? " + JsonConvert.SerializeObject(lt));
                    if (lt != null) {
                        _lockTarget = new VectorOfPointF(lt);
                        var lC = 0;
                        while (lC < 20) {
                            _targets.Add(VPointFToVPoint(_lockTarget));
                            lC++;
                        }
                    }
                } catch (Exception e) {
                    LogUtil.Write("Exception: " + e.Message);
                }
            }

            var tl = new Point(0, 0);
            var tr = new Point(_scaleWidth, 0);
            var br = new Point(_scaleWidth, _scaleHeight);
            var bl = new Point(0, _scaleHeight);
            _showSource = DataUtil.GetItem<bool>("showSource") ?? false;
            _showEdged = DataUtil.GetItem<bool>("showEdged") ?? false;
            _showWarped = DataUtil.GetItem<bool>("showWarped") ?? false;
            _vectors = new PointF[] {tl, tr, br, bl};
            _frameCount = 0;
            LogUtil.Write("Start Capture should be running...");
        }

        private IVideoStream GetCamera() {
            if (_captureMode != 0 && _captureMode != 1) {
                switch (_captureMode) {
                    case 2:
                        LogUtil.Write("Definitely grabbing HDMI video stream here.");
                        return new HdmiVideoStream();
                    case 3:
                        LogUtil.Write("Definitely grabbing screen here.");
                        return new ScreenVideoStream();
                }
            }

            switch (_camType) {
                case 0:
                    // 0 = pi module, 1 = web cam
                    LogUtil.Write("Loading Pi cam.");
                    var camMode = DataUtil.GetItem<int>("camMode") ?? 1;
                    return new PiVideoStream(_camWidth, _camHeight, camMode);
                case 1:
                    LogUtil.Write("Loading web cam.");
                    return new WebCamVideoStream(0);
            }

            return null;
        }

        public Task StartCapture(DreamClient dreamClient, CancellationToken cancellationToken) {
            return Task.Run(() => {
                LogUtil.WriteInc($"Starting capture task, setting sw and h to {_scaleWidth} and {_scaleHeight}");
                _splitter = new Splitter(_ledData, _scaleWidth, _scaleHeight);
                LogUtil.Write("Splitter is created.");
                LogUtil.Write("VC Started.");
                while (!cancellationToken.IsCancellationRequested) {
                    var frame = _vc.Frame;
                    if (frame == null) {
                        LogUtil.Write("Frame is null, dude.", "WARN");
                        continue;
                    }

                    if (frame.Cols == 0) {
                        LogUtil.Write("Frame has no columns, dude.", "WARN");
                        continue;
                    }
                    var warped = ProcessFrame(frame);
                    if (warped == null) {
                        LogUtil.Write("Unable to process frame, Dude.", "WARN");
                        continue;
                    }
                    _splitter.Update(warped);
                    var colors = _splitter.GetColors();
                    var sectors = _splitter.GetSectors();
                    var sectors3 = _splitter.GetSectorsV2();
                    dreamClient.SendColors(colors, sectors, sectors3);
                }

                LogUtil.Write("Capture task completed!", "WARN");
                return Task.CompletedTask;
            }, cancellationToken);
        }

        private Mat ProcessFrame(Mat input) {
            Mat output;
            // If we need to crop our image...do it.
            if (_captureMode == 0 || _captureMode == 1) {
                // Crop our camera frame if the input type is not HDMI
                output = CamFrame(input);
                // Save a preview frame every 450 frames
                if (_frameCount == 0 || _frameCount == 450 || _frameCount == 900) {
                    var path = Directory.GetCurrentDirectory();
                    input?.Save(path + "/wwwroot/img/_preview_input.jpg");
                    output?.Save(path + "/wwwroot/img/_preview_output.jpg");
                }
                // Otherwise, just return the input.
            } else {
                output = input;
            }

            // Increment our frame counter
            if (_frameCount >= 900) {
                _frameCount = 0;
            } else {
                _frameCount++;
            }

            return output;
        }


        private Mat CamFrame(Mat input) {
            Mat output = null;
            var scaled = new Mat();

            // Check to see if we actually have to scale down and do it
            if (Math.Abs(_scaleFactor - 1.0f) > .0001) {
                CvInvoke.Resize(input, scaled, _scaleSize);
            } else {
                scaled = input.Clone();
            }

            // If we don't have a target, find one
            if (_lockTarget == null) {
                _lockTarget = FindTarget(scaled);
                if (_lockTarget != null) {
                    LogUtil.Write("Target hit.");
                    DataUtil.SetItem<PointF[]>("lockTarget", _lockTarget.ToArray());
                } else {
                    LogUtil.Write("No target.");
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

            if (_showEdged) CvInvoke.Imshow("Source", cannyEdges);
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
            for (int i = 0; i < ta.Length; i++) pIn[i] = new Point((int) ta[i].X, (int) ta[i].Y);
            return new VectorOfPoint(pIn);
        }


        private static PointF[] SortPoints(VectorOfPoint wTarget) {
            var ta = wTarget.ToArray();
            var pIn = new PointF[wTarget.Size];
            for (int i = 0; i < ta.Length; i++) pIn[i] = ta[i];
            // Order points?
            var tPoints = pIn.OrderBy(p => p.Y);
            var vPoints = pIn.OrderByDescending(p => p.Y);
            var vtPoints = tPoints.Take(2);
            var vvPoints = vPoints.Take(2);
            vtPoints = vtPoints.OrderBy(p => p.X);
            vvPoints = vvPoints.OrderByDescending(p => p.X);
            PointF tl = vtPoints.ElementAt(0);
            PointF tr = vtPoints.ElementAt(1);
            PointF br = vvPoints.ElementAt(0);
            PointF bl = vvPoints.ElementAt(1);
            PointF[] outPut = {tl, tr, br, bl};
            return outPut;
        }
    }
}