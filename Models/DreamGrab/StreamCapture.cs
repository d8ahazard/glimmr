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
using HueDream.Models.DreamScreen;
using HueDream.Models.Util;
using Newtonsoft.Json;

namespace HueDream.Models.DreamGrab {
    public sealed class StreamCapture
    {
        private int scaleHeight = 400;
        private int scaleWidth = 600;
        private int camWidth;
        private int camHeight;
        private float scaleFactor;
        private bool showSource;
        private bool showEdged;
        private bool showWarped;
        private PointF[] vectors;
        private int camType;
        private int srcArea;
        private Size scaleSize;
        private LedData ledData;
        private int frameCount;
        private DreamClient dc;
        private VectorOfPointF lockTarget;
        private List<VectorOfPoint> targets;
        private IVideoStream vc;
        private Splitter splitter;
        private Mat k;
        private Mat d;


        public StreamCapture(CancellationToken camToken) {
            LogUtil.WriteInc("Initializing stream capture.");
            targets = new List<VectorOfPoint>();
            SetCapVars();
            vc = GetCamera();
            vc.Start(camToken);
        }

        private void SetCapVars() {
            ledData = DreamData.GetItem<LedData>("ledData");
            camType = DreamData.GetItem<int>("camType");
            camWidth = DreamData.GetItem<int>("camWidth") ?? 1920;
            camHeight = DreamData.GetItem<int>("camHeight") ?? 1080;
            scaleFactor = DreamData.GetItem<float>("scaleFactor") ?? .5f;
            scaleWidth = Convert.ToInt32(camWidth * scaleFactor);
            scaleHeight = Convert.ToInt32(camHeight * scaleFactor);
            srcArea = scaleWidth * scaleHeight;
            scaleSize = new Size(scaleWidth, scaleHeight);
            if (camType == 0) {
                var kStr = DreamData.GetItem("k");
                if (kStr == null) {
                    LogUtil.Write("Running static camera calibration.");
                    Calibrate.ProcessFrames();
                } else {
                    LogUtil.Write("Camera calibration settings loaded.");
                }
            
                kStr = DreamData.GetItem("k");
                var dStr = DreamData.GetItem("d");
                k = JsonConvert.DeserializeObject<Mat>(kStr);
                d = JsonConvert.DeserializeObject<Mat>(dStr);
                LogUtil.Write("calib vars deserialized.");
            }
            try {
                var lt = DreamData.GetItem<PointF[]>("lockTarget");
                LogUtil.Write("LT Grabbed? " + JsonConvert.SerializeObject(lt));
                if (lt != null) {
                    lockTarget = new VectorOfPointF(lt);
                    var lC = 0;
                    while (lC < 20) {
                        targets.Add(VPointFToVPoint(lockTarget));
                        lC++;
                    }
                }
            } catch (Exception e) {
                LogUtil.Write("Exception: " + e.Message);
            }

            var tl = new Point(0, 0);
            var tr = new Point(scaleWidth, 0);
            var br = new Point(scaleWidth, scaleHeight);
            var bl = new Point(0, scaleHeight);
            showSource = DreamData.GetItem<bool>("showSource") ?? false;
            showEdged = DreamData.GetItem<bool>("showEdged") ?? false;
            showWarped = DreamData.GetItem<bool>("showWarped") ?? false;
            vectors = new PointF[] { tl, tr, br, bl };
            frameCount = 0;
            LogUtil.Write("Start Capture should be running...");
        }

        private IVideoStream GetCamera() {
            var capMode = DreamData.GetItem<int>("captureMode");
            if (capMode != 1 && capMode != 2)
                return capMode == 3 ? new CaptureVideoStream() : null;
            switch (camType) {
                case 0:
                    // 0 = pi module, 1 = web cam, 3 = capture?
                    LogUtil.Write("Loading Pi cam.");
                    var camMode = DreamData.GetItem<int>("camMode") ?? 1;
                    return new PiVideoStream(camWidth, camHeight, camMode);
                case 1:
                    LogUtil.Write("Loading web cam.");
                    return new WebCamVideoStream(0);
            }

            return null;
        }

        public Task StartCapture(DreamClient dreamClient, CancellationToken cancellationToken) {
            return Task.Run(() => {
                LogUtil.WriteInc("Starting capture task.");
                splitter = new Splitter(ledData, scaleWidth, scaleHeight);
                while (!cancellationToken.IsCancellationRequested) {                    
                    var frame = vc.frame;
                    if (frame == null) continue;
                    if (frame.Cols == 0) continue;
                    var warped = ProcessFrame(frame);
                    if (warped == null) continue;
                    splitter.Update(warped);
                    var colors = splitter.GetColors();
                    var sectors = splitter.GetSectors();
                    dreamClient.SendColors(colors, sectors);
                }
                LogUtil.WriteDec("Capture task completed.");
                return Task.CompletedTask;
            }, cancellationToken);
        }

        private Mat ProcessFrame(Mat input) {
            Mat output = null;
            if (camType != 3) {
                // Crop our camera frame if the input type is not HDMI
                output = CamFrame(input);
                // Save a preview frame every 450 frames
                if (frameCount == 0 || frameCount == 450 || frameCount == 900) {
                    var path = Directory.GetCurrentDirectory();
                    input?.Save(path + "/wwwroot/img/_preview_input.jpg");
                    output?.Save(path + "/wwwroot/img/_preview_output.jpg");
                }
            }
            
            // Increment our frame counter
            if (frameCount >= 900) {
                frameCount = 0;
            } else {
                frameCount++;
            }

            return output;
        }

        
        private Mat CamFrame(Mat input) {
            Mat output = null;
            var scaled = new Mat();
            
            // Check to see if we actually have to scale down and do it
            if (Math.Abs(scaleFactor - 1.0f) > .0001) {
                CvInvoke.Resize(input, scaled, scaleSize);
            } else {
                scaled = input.Clone();
            }

            // If we don't have a target, find one
            if (lockTarget == null) {
                lockTarget = FindTarget(scaled);
                if (lockTarget != null) {
                    LogUtil.Write("Target hit.");
                    DreamData.SetItem<PointF[]>("lockTarget", lockTarget.ToArray());
                } else {
                    LogUtil.Write("No target.");
                }
            }
            
            // If we do or we found one...crop it out
            if (lockTarget != null) {
                var dPoints = lockTarget.ToArray();
                var warpMat = CvInvoke.GetPerspectiveTransform(dPoints, vectors);
                output = new Mat();
                CvInvoke.WarpPerspective(scaled, output, warpMat, scaleSize);
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

            if (showEdged) CvInvoke.Imshow("Source", cannyEdges);
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
                    if (approxContour.Size == 4) {
                        var cntArea = CvInvoke.ContourArea(approxContour);
                        if (cntArea / srcArea > .15) {
                            var pointOut = new VectorOfPointF(SortPoints(approxContour));
                            targets.Add(VPointFToVPoint(pointOut));
                        }
                    }
                }
                if (showEdged) {
                    var color = new MCvScalar(255, 255, 0);
                    CvInvoke.DrawContours(input, contours, -1, color);
                    CvInvoke.Imshow("Edged", cannyEdges);
                }
            }

            var output = CountTargets(targets);
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
                targets.RemoveRange(0, 150);
            }
            return output;
        }

        private VectorOfPoint VPointFToVPoint(VectorOfPointF input) {
            var ta = input.ToArray();
            var pIn = new Point[input.Size];
            for (int i = 0; i < ta.Length; i++) pIn[i] = new Point((int) ta[i].X, (int) ta[i].Y);
            return new VectorOfPoint(pIn);
        }
       

        private PointF[] SortPoints(VectorOfPoint wTarget) {
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
            PointF[] outPut = { tl, tr, br, bl };
            return outPut;
        }

    }
}