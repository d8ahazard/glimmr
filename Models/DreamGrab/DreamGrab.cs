using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
    public class DreamGrab {
        private int lockCount;
        private VectorOfPoint target;
        private int ScaleHeight = 400;
        private int ScaleWidth = 600;
        private int camWidth;
        private int camHeight;
        private float scaleFactor;
        private bool showSource;
        private bool showEdged;
        private bool showWarped;
        private bool targetLocked;
        private PointF[] vectors;
        private int camType;
        private LedData ledData;

        private readonly DreamClient dc;
        private VectorOfPoint lockTarget;
        private LedStrip strip;
        private CancellationTokenSource cts;


        public DreamGrab(DreamClient client) {
            LogUtil.Write("Dreamgrab Initialized.");
            dc = client;
            setCapVars();
            LogUtil.Write("Init done?");
        }

        private void setCapVars() {
            ledData = DreamData.GetItem<LedData>("ledData");
            camType = DreamData.GetItem<int>("camType");
            camWidth = DreamData.GetItem<int>("camWidth") ?? 1920;
            camHeight = DreamData.GetItem<int>("camHeight") ?? 1080;
            scaleFactor = DreamData.GetItem<float>("scaleFactor") ?? .5;
            ScaleWidth = Convert.ToInt32(camWidth * scaleFactor);
            ScaleHeight = Convert.ToInt32(camHeight * scaleFactor);
            var tl = new Point(0, 0);
            var tr = new Point(ScaleWidth, 0);
            var br = new Point(ScaleWidth, ScaleHeight);
            var bl = new Point(0, ScaleHeight);
            showSource = DreamData.GetItem<bool>("showSource") ?? false;
            showEdged = DreamData.GetItem<bool>("showEdged") ?? false;
            showWarped = DreamData.GetItem<bool>("showWarped") ?? false;
            vectors = new PointF[] { tl, tr, br, bl };
        }

        private IVideoStream GetCamera() {
            if (dc.CaptureMode != 1 && dc.CaptureMode != 2)
                return dc.CaptureMode == 3 ? new CaptureVideoStream() : null;
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

        public Task StartCapture(CancellationToken cancellationToken) {
            return Task.Run(() => {
                setCapVars();
                LogUtil.Write("Start Capture should be running...");
                var cam = GetCamera();
                Task.Run(() => cam.Start(cancellationToken));
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
            int srcArea = ScaleWidth * ScaleHeight;
            var scaleSize = new Size(ScaleWidth, ScaleHeight);
            LogUtil.Write("Input dimensions are " + input.Width + " and " + input.Height);
            if (showSource) CvInvoke.Imshow("Source", input);
            CvInvoke.Resize(input, scaled, scaleSize);

            // If target isn't locked, then find it.
            if (!targetLocked) {
                LogUtil.Write("No target, looking for stuff.");
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
                        if (!(cntArea / srcArea > .15)) continue;
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
                LogUtil.Write("Edged width is " + cannyEdges.Width + " and " + cannyEdges.Height);
                if (showEdged) CvInvoke.Imshow("Edged", cannyEdges);
                cannyEdges.Dispose();
                uImage.Dispose();
                gray.Dispose();
            }

            if (targetLocked && target != null) {
                var warpMat = GetWarp(target);
                var outImg = new Mat();
                CvInvoke.WarpPerspective(scaled, outImg, warpMat, scaleSize);
                warped = outImg;
                if (showWarped) CvInvoke.Imshow("Warped!", warped);
            }

            if (showWarped || showEdged || showSource)
            {
                var c = CvInvoke.WaitKey(1);
                if (c == 'c')
                {
                    LogUtil.Write("C pressed.");
                    Process.GetCurrentProcess().Kill();
                }
                else
                {
                    if (c != -1)
                    {
                        LogUtil.Write(c + " pressed.");
                    }
                }
            }
            scaled.Dispose();
            return warped;
        }


        private Mat GetWarp(VectorOfPoint wTarget) {
            var ta = wTarget.ToArray();
            var pointOut = new PointF[wTarget.Size];
            for (int i = 0; i < ta.Length; i++) pointOut[i] = ta[i];
            // Order points?
            pointOut = SortPoints(pointOut);            
            var warpMat = CvInvoke.FindHomography(vectors, pointOut.ToArray());
            var homogMat = new Mat();
            CvInvoke.Invert(warpMat, homogMat, DecompMethod.LU);
            return homogMat;
        }

        private PointF[] SortPoints(PointF[] pIn) {
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
            PointF[] outPut = new PointF[] { tl, tr, br, bl };
            return outPut;
        }

        



    }
}