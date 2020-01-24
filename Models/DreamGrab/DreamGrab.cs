using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.UI;
using Emgu.CV.Util;
using HueDream.Models.DreamScreen;
using HueDream.Models.Util;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Point = System.Drawing.Point;

namespace HueDream.Models.DreamGrab {
        
    public class DreamGrab {
        private int lockCount;
        private VectorOfPoint lockTarget;
        private VectorOfPoint target;
        private static int scale_height = 300;
        private static int scale_width = 400;
        private bool targetLocked;
        private LedData ledData;
        private LedStrip strip;
        private CancellationTokenSource cts;
        private PointF[] vects;
        private DreamClient dc;

        public DreamGrab(DreamClient client) {
            LogUtil.Write("Dreamgrab Initialized.");
            dc = client;
            ledData = DreamData.GetItem<LedData>("ledData");
            var tl = new Point(0, 0);
            var tr = new Point(scale_width, 0);
            var br = new Point(scale_width, scale_height);
            var bl = new Point(0, scale_height);
            vects = new PointF[]{ tl, tr, br, bl };

            LogUtil.Write("Init done?");
        }

        public IVideoStream GetCamera() {
            if (ledData.CamType == 0) { // 0 = pi module, 1 = webcam, 3 = capture?
                LogUtil.Write("Loading Pi cam.");
                return new PiVideoStream();
            } else if (ledData.CamType == 1) {
                LogUtil.Write("Loading webcam.");
                return new WebCamVideoStream(ledData.StreamId);
            } else {
                return new CaptureVideoStream();
            }
        }        

        public Task StartCapture(CancellationToken cancellationToken) {
            return Task.Run(() => {
                LogUtil.Write("Start Capture should be running...");
                var cam = GetCamera();
                cam.Start(cancellationToken);
                LogUtil.Write("Cam started?");
                var splitter = new Splitter(ledData, scale_width, scale_height);
                LogUtil.Write("Splitter created.");
                while (true) {
                    var frame = cam.frame;                    
                    if (frame != null) {
                        if (frame.Bitmap == null) continue;
                        var warped = ProcessFrame(frame);
                        if (warped != null) {
                            var colors = splitter.GetColors(warped);
                            dc.SendColors(colors);
                        }
                    } else {
                        LogUtil.Write("Frame is null?");
                    }
                }
            });
        }

        private Mat ProcessFrame(Mat input) {
            if (ledData.CamType != 3) {
                return GetScreen(input);
            } else {
                return input;
            }
        }

        
        private Mat GetScreen(Mat input) {
            Mat warped = null;            
            VectorOfPoint approxContour = new VectorOfPoint();
            Mat scaled = new Mat();            
            var srcArea = scale_width * scale_height;
            CvInvoke.Resize(input, scaled, new Size(scale_width, scale_height), 0, 0, Inter.Linear);

            // If target isn't locked, then find it.
            if (!targetLocked) {
                target = null;
                Mat cannyEdges = new Mat();
                Mat uimage = new Mat();
                Mat gray = new Mat();
                // Convert to greyscale
                CvInvoke.CvtColor(scaled, uimage, ColorConversion.Bgr2Gray);
                CvInvoke.BilateralFilter(uimage, gray, 11, 17, 17);
                // Get edged version
                double cannyThreshold = 0.0;
                double cannyThresholdLinking = 200.0;
                CvInvoke.Canny(gray, cannyEdges, cannyThreshold, cannyThresholdLinking);
                // Get contours
                using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint()) {
                    MCvScalar color = new MCvScalar(255, 255, 0);
                    CvInvoke.DrawContours(scaled, contours, -1, color);
                    CvInvoke.FindContours(cannyEdges, contours, null, RetrType.List, ChainApproxMethod.ChainApproxSimple);
                    int count = contours.Size;
                    // Looping contours
                    for (int i = 0; i < count; i++) {
                        using (VectorOfPoint contour = contours[i]){
                            CvInvoke.ApproxPolyDP(contour, approxContour, CvInvoke.ArcLength(contour, true) * 0.02, true);
                            var cntArea = CvInvoke.ContourArea(approxContour, false);
                            LogUtil.Write($@"Contour area is {cntArea}, source area is {srcArea}.");
                            if (cntArea / srcArea > .25) //only consider contours with area greater than 25% total screen
                            {
                                if (approxContour.Size == 4) //The contour has 4 vertices.
                                {
                                    Console.WriteLine("We have a big box.");
                                    lockCount++;
                                    if (lockCount > 30) {
                                        LogUtil.Write("TARGET LOCKED!");
                                        targetLocked = true;
                                        target = approxContour;
                                        break;
                                    }                                    
                                }
                            }
                        }
                    }
                }
                //CvInvoke.Imshow("Input", scaled);
                CvInvoke.Imshow("Edged", cannyEdges);
                cannyEdges.Dispose();
                uimage.Dispose();
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


        private Mat GetWarp(VectorOfPoint target) {            
            var ta = target.ToArray();
            var pointList = new List<PointF>();
            foreach (PointF p in ta) {
                pointList.Add(p);
            }
            var pointArray = pointList.ToArray();
            var warpMat = CvInvoke.FindHomography(pointArray, vects, RobustEstimationAlgorithm.AllPoints);
            return warpMat;
        }
       
        
        private void UpdateStrip(Color[] colors) {
            if (strip != null) {
                strip.UpdateAll(colors);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) {
            try {
                Console.WriteLine(@"StopAsync called.");
                cts.Cancel();
            } finally {
                LogUtil.WriteDec(@"DreamGrabber done.");
            }
            return Task.CompletedTask;
        }
    }
}