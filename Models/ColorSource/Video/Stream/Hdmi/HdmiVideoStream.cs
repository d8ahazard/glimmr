using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Glimmr.Models.Util;
using Serilog;

namespace Glimmr.Models.ColorSource.Video.Stream.Hdmi {
    public class HdmiVideoStream : IVideoStream, IDisposable
    {
        private readonly VideoCapture _video;
        public Mat Frame { get; set; }
        private bool _disposed;

        
        public HdmiVideoStream(int inputStream) {
            var capType = VideoCapture.API.DShow;
            capType = VideoCapture.API.V4L;
            _video = new VideoCapture(inputStream, capType);
            _video.SetCaptureProperty(CapProp.FrameWidth, 640);
            _video.SetCaptureProperty(CapProp.FrameHeight, 480);
            Log.Debug("Scaling HDMI to 640x480");
            var foo = _video.CaptureSource.ToString();
            Frame = new Mat();
            Log.Debug("Stream init, capture source is " + foo + ", " + inputStream);            
        }

        private void SetFrame(object sender, EventArgs e) {
            if (_video != null && _video.Ptr != IntPtr.Zero) {
                //Log.Debug("Setting frame??");
                _video.Read(Frame);
            } else {
                Log.Debug("No frame to set...");
            }
        }

        public static int[] ListSources() {
            var i = 0;
            var output = new List<int>();
            while (i < 10) {
                try {
                    // Check if video stream is available.
                    var v = new VideoCapture(i); // Will crash if not available, hence try/catch.
                    var w = v.Width;
                    var h = v.Height;
                    if (w != 0 && h != 0) {
                        Log.Debug($"Width, height of {i}: {w}, {h}");

                        output.Add(i);
                    }

                    v.Dispose();
                } catch (Exception e) {
                    Log.Debug("Exception with cam " + i + ": " + e);
                }

                i++;
            }
            return output.ToArray();
        }

        public async Task Start(CancellationToken ct) {
            Log.Debug("Starting HDMI stream...");
            _video.ImageGrabbed += SetFrame;
            _video.Start();
            Log.Debug("HDMI Stream started.");
        }

        public void Dispose() {
            if (_disposed) return;
            _disposed = true;
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposing) return;
            Frame.Dispose();
            _video.Dispose();
        }
    }
}