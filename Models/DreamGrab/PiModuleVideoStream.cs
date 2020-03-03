using Emgu.CV;
using Emgu.CV.Structure;
using HueDream.Models.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unosquare.RaspberryIO;
using Unosquare.RaspberryIO.Camera;

namespace HueDream.Models.DreamGrab
{
    public class PiModuleVideoStream : IVideoStream {
        public Mat Frame;
        private int capWidth;
        private int capHeight;
        Mat IVideoStream.frame { get => Frame; set => Frame = value; }
        public PiModuleVideoStream(int camWidth, int camHeight) {
            capWidth = camWidth;
            capHeight = camHeight;
        }

        
        
        public async Task Start(CancellationToken ct) {
            LogUtil.Write("Starting pi video stream");
            // Setup our working variables
            var videoByteCount = 0;
            var videoEventCount = 0;
            var startTime = DateTime.UtcNow;

            // Configure video settings
            var videoSettings = new CameraVideoSettings() {
                CaptureTimeoutMilliseconds = 2000,
                CaptureDisplayPreview = false,
                ImageFlipVertically = false,
                CaptureExposure = CameraExposureMode.Backlight,
                CaptureWidth = capWidth,
                CaptureHeight = capHeight
            };

            try {
                LogUtil.Write("Opening camera");
                // Start the video recording
                Pi.Camera.OpenVideoStream(videoSettings,
                     data => { 
                        LogUtil.Write("We have cam data.");
                        var img = new Image<Bgr, byte>(capWidth, capHeight);
                        LogUtil.Write("Img created.");
                        img.Bytes = data;
                        LogUtil.Write("Bytes set.");
                        Frame = img.Mat;
                        LogUtil.Write("Frame stored.");
                    });
                LogUtil.Write("Camera is opened.");
                while(!ct.IsCancellationRequested) {
                    
                }
                LogUtil.Write("Capture stopped.");
            }
            catch (Exception ex) {
                Console.WriteLine($"{ex.GetType()}: {ex.Message}");
            } finally {
                // Always close the video stream to ensure raspivid quits

                Pi.Camera.CloseVideoStream();
                LogUtil.Write("Camera closed.");

                
            }
        }
    }
}
