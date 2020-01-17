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
using HueDream.Models.Util;
using Microsoft.Extensions.Hosting;
using Point = System.Drawing.Point;

namespace HueDream.Models.DreamGrab {
        
    public class DreamGrab {
        private Mat orig_frame;
        private Mat edged_frame;
        private Mat warped_frame;
        private VectorOfPoint curr_target;
        private VectorOfPoint prev_target;
        private int lockCount;
        private Mat curr_edged;
        private Mat prev_edged;
        private static int scale_height = 300;
        private static int scale_width = 400;
        private int camType;
        private int lineSensitivity;
        private int avgThreshold;
        private bool targetLocked;
        private LedData ledData;
        private LedStrip strip;
        private IOutputArray _foo;
        private CancellationTokenSource cts;
        private Task captureTask;

        public DreamGrab() {
            LogUtil.Write("Dreamgrab Initialized.");
            var dd = DreamData.GetStore();
            ledData = dd.GetItem<LedData>("ledData") ?? new LedData(true);
            if (ledData.UseLed) {
                strip = new LedStrip(ledData);
            }
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
                    LogUtil.Write("Grabbing frame");
                    var frame = cam.frame;                    
                    if (frame != null) {
                        if (frame.Bitmap == null) continue;
                        LogUtil.Write("We have a frame");
                        var warped = ProcessFrame(frame);
                        if (warped != null) {
                            var colors = splitter.GetColors(warped);
                            LogUtil.Write("Colors grabbed too!");
                            UpdateStrip(colors);
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
            if (!targetLocked) {
                LogUtil.Write("Target is not locked, finding target.");
                var srcArea = scale_width * scale_height;
                var edgeColor = Color.Red;                
                Console.WriteLine("Resizing");
                Mat scaled = new Mat();
                Console.WriteLine("Resizing2");
                CvInvoke.Resize(input, scaled, new Size(scale_width, scale_height), 0, 0, Inter.Linear);
                //CvInvoke.WaitKey(1);
                Console.WriteLine("Resized");
                //Load the image from file and resize it for display
                //Convert the image to grayscale and filter out the noise
                Mat uimage = new Mat();
                Mat gray = new Mat();
                Console.WriteLine("GreyScaling");
                CvInvoke.CvtColor(scaled, uimage, ColorConversion.Bgr2Gray);
                CvInvoke.BilateralFilter(uimage, gray, 11, 17, 17);
                //use image pyr to remove noise
                Mat cannyEdges = new Mat();
                double cannyThreshold = 10.0;
                double cannyThresholdLinking = 200.0;
                Console.WriteLine("Canny");
                CvInvoke.Canny(gray, cannyEdges, cannyThreshold, cannyThresholdLinking);
                Stopwatch watch = Stopwatch.StartNew();
                #region Find rectangles
                List<RotatedRect> boxList = new List<RotatedRect>(); //a box is a rotated rectangle
                
                using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint()) {
                    MCvScalar color = new MCvScalar(255, 255, 0);
                    CvInvoke.DrawContours(scaled, contours, -1, color);
                    CvInvoke.FindContours(cannyEdges, contours, null, RetrType.List, ChainApproxMethod.ChainApproxSimple);
                    int count = contours.Size;
                    Console.WriteLine($@"Looping contours, we have {count}");
                    for (int i = 0; i < count; i++) {
                        using (VectorOfPoint contour = contours[i])
                        using (VectorOfPoint approxContour = new VectorOfPoint()) {
                            CvInvoke.ApproxPolyDP(contour, approxContour, CvInvoke.ArcLength(contour, true) * 0.02, true);
                            
                            if (CvInvoke.ContourArea(approxContour, false) / srcArea > .35) //only consider contours with area greater than 250
                            {
                                if (approxContour.Size == 4) //The contour has 4 vertices.
                                {
                                    Console.WriteLine("We have a big box.");
                                    #region determine if all the angles in the contour are within [80, 100] degree
                                    bool isRectangle = true;
                                    Point[] pts = approxContour.ToArray();
                                    LineSegment2D[] edges = PointCollection.PolyLine(pts, true);

                                    for (int j = 0; j < edges.Length; j++) {
                                        double angle = Math.Abs(
                                           edges[(j + 1) % edges.Length].GetExteriorAngleDegree(edges[j]));
                                        if (angle < 60 || angle > 120) {
                                            Console.WriteLine("This is not a rectangle.");
                                            isRectangle = false;
                                            break;
                                        }
                                    }
                                    #endregion

                                    if (isRectangle) {
                                        curr_target = contour;
                                        lockCount++;
                                        if (lockCount > 30) {
                                            targetLocked = true;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        if (curr_target != null) {
                            prev_target = curr_target;
                            var warped = new Mat();
                            warped = ImUtil.FourPointTransform(input, curr_target);
                        }
                    }
                    CvInvoke.Imshow("Input", scaled);
                    CvInvoke.WaitKey(1);
                }

                watch.Stop();
            }
            return null;
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
    #endregion
}