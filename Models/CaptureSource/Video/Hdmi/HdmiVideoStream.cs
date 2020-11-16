using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Glimmr.Models.Util;

namespace Glimmr.Models.CaptureSource.Video.Hdmi {
    public class HdmiVideoStream : IVideoStream, IDisposable
    {
        private readonly VideoCapture _video;
        public Mat Frame;
        private bool _disposed;

        Mat IVideoStream.Frame {
            get => Frame;
            set => Frame = value;
        }

        public HdmiVideoStream(int inputStream) {
            var capType = VideoCapture.API.DShow;
            capType = VideoCapture.API.V4L;
            _video = new VideoCapture(inputStream, capType);
            _video.SetCaptureProperty(CapProp.FrameWidth, 640);
            _video.SetCaptureProperty(CapProp.FrameHeight, 480);
            LogUtil.Write("Set cap props to 640x480");
            var foo = _video.CaptureSource.ToString();
            Frame = new Mat();
            LogUtil.Write("Stream init, capture source is " + foo + ", " + inputStream);            
        }

        private void SetFrame(object sender, EventArgs e) {
            if (_video != null && _video.Ptr != IntPtr.Zero) {
                //LogUtil.Write("Setting frame??");
                _video.Read(Frame);
            } else {
                LogUtil.Write("No frame to set...");
            }
        }

        public static int[] ListCameras() {
            var i = 0;
            var output = new List<int>();
            while (i < 10) {
                try {
                    // Check if camera is available.
                    var v = new VideoCapture(i); // Will crash if not available, hence try/catch.
                    var w = v.Width;
                    var h = v.Height;
                    if (w != 0 && h != 0) {
                        LogUtil.Write($"Width, height of {i}: {w}, {h}");

                        output.Add(i);
                    }

                    v.Dispose();
                } catch (Exception e) {
                    LogUtil.Write("Exception with cam " + i + ": " + e);
                }

                i++;
            }
            return output.ToArray();
        }

        public async Task Start(CancellationToken ct) {
            LogUtil.Write("WebCam Stream started.");
            _video.ImageGrabbed += SetFrame;
            _video.Start();
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