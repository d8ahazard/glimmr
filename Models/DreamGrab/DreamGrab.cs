using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using HueDream.Models.DreamScreen;
using HueDream.Models.Util;

namespace HueDream.Models.DreamGrab {
    public class DreamGrab {
        private int lockCount;
        private VectorOfPoint target;
        private const int ScaleHeight = 300;
        private const int ScaleWidth = 400;
        private bool targetLocked;
        private readonly LedData ledData;
        private readonly PointF[] vectors;
        private readonly DreamClient dc;
        private readonly int camType;
        private VectorOfPoint lockTarget;
        private LedStrip strip;
        private CancellationTokenSource cts;


        public DreamGrab(DreamClient client) {
            LogUtil.Write("Dreamgrab Initialized.");
            dc = client;
            ledData = DreamData.GetItem<LedData>("ledData");
            camType = DreamData.GetItem<int>("camType");
            var tl = new Point(0, 0);
            var tr = new Point(ScaleWidth, 0);
            var br = new Point(ScaleWidth, ScaleHeight);
            var bl = new Point(0, ScaleHeight);
            vectors = new PointF[] {tl, tr, br, bl};

            LogUtil.Write("Init done?");
        }

        private IVideoStream GetCamera() {
            if (dc.CaptureMode != 1 && dc.CaptureMode != 2)
                return dc.CaptureMode == 3 ? new CaptureVideoStream() : null;
            switch (camType) {
                case 0:
                    // 0 = pi module, 1 = web cam, 3 = capture?
                    LogUtil.Write("Loading Pi cam.");
                    return new PiVideoStream();
                case 1:
                    LogUtil.Write("Loading web cam.");
                    return new WebCamVideoStream(0);
            }

            return null;
        }

        public Task StartCapture(CancellationToken cancellationToken) {
            return Task.Run(() => {
                LogUtil.Write("Start Capture should be running...");
                var cam = GetCamera();
                cam.Start(cancellationToken);
                LogUtil.Write("Cam started?");
                var splitter = new Splitter(ledData, ScaleWidth, ScaleHeight);
                LogUtil.Write("Splitter created.");
                while (!cancellationToken.IsCancellationRequested) {
                    var frame = cam.frame;
                    if (frame != null) {
                        if (frame.Cols == 0) continue;
                        var warped = ProcessFrame(frame);
                        if (warped == null) continue;
                        var colors = splitter.GetColors(warped);
                        var sectors = splitter.GetSectors(warped);
                        dc.SendColors(colors, sectors);
                    } else {
                        LogUtil.Write("Frame is null?");
                    }
                }
                return Task.CompletedTask;
            });
        }

        private Mat ProcessFrame(Mat input) {
            if (camType != 3) {
                return GetScreen(input);
            }

            return input;
        }


        private Mat GetScreen(Mat input) {
            Mat warped = null;
            var approxContour = new VectorOfPoint();
            var scaled = new Mat();
            const int srcArea = ScaleWidth * ScaleHeight;
            CvInvoke.Resize(input, scaled, new Size(ScaleWidth, ScaleHeight));

            // If target isn't locked, then find it.
            if (!targetLocked) {
                target = null;
                var cannyEdges = new Mat();
                var uImage = new Mat();
                var gray = new Mat();
                // Convert to greyscale
                CvInvoke.CvtColor(scaled, uImage, ColorConversion.Bgr2Gray);
                CvInvoke.BilateralFilter(uImage, gray, 11, 17, 17);
                // Get edged version
                const double cannyThreshold = 0.0;
                const double cannyThresholdLinking = 200.0;
                CvInvoke.Canny(gray, cannyEdges, cannyThreshold, cannyThresholdLinking);
                // Get contours
                using (var contours = new VectorOfVectorOfPoint()) {
                    var color = new MCvScalar(255, 255, 0);
                    CvInvoke.DrawContours(scaled, contours, -1, color);
                    CvInvoke.FindContours(cannyEdges, contours, null, RetrType.List,
                        ChainApproxMethod.ChainApproxSimple);
                    var count = contours.Size;
                    // Looping contours
                    for (var i = 0; i < count; i++) {
                        using var contour = contours[i];
                        CvInvoke.ApproxPolyDP(contour, approxContour, CvInvoke.ArcLength(contour, true) * 0.02,
                            true);
                        var cntArea = CvInvoke.ContourArea(approxContour);
                        LogUtil.Write($@"Contour area is {cntArea}, source area is {srcArea}.");
                        if (!(cntArea / srcArea > .25)) continue;
                        if (approxContour.Size != 4) continue;
                        Console.WriteLine("We have a big box.");
                        lockCount++;
                        if (lockCount <= 30) continue;
                        LogUtil.Write("TARGET LOCKED!");
                        targetLocked = true;
                        target = approxContour;
                        break;
                    }
                }

                CvInvoke.Imshow("Edged", cannyEdges);
                cannyEdges.Dispose();
                uImage.Dispose();
                gray.Dispose();
            }

            if (targetLocked && target != null) {
                var warpMat = GetWarp(target);
                var scaleSize = new Size(scaled.Cols, scaled.Rows);
                var outImg = new Mat();
                CvInvoke.WarpPerspective(scaled, outImg, warpMat, scaleSize);
                warped = outImg;
                CvInvoke.Imshow("Warped!", warped);
            }

            CvInvoke.WaitKey(1);
            scaled.Dispose();
            return warped;
        }


        private Mat GetWarp(VectorOfPoint wTarget) {
            var ta = wTarget.ToArray();
            var pointList = new List<PointF>();
            foreach (PointF p in ta) {
                pointList.Add(p);
            }
            var pointArray = pointList.ToArray();
            var warpMat = CvInvoke.FindHomography(pointArray, vectors);
            return warpMat;
        }


      
    }
}