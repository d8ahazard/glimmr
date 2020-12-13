using System;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Glimmr.Models.Util;

namespace Glimmr.Models.ColorSource.Video.Stream.WebCam {
    public class WebCamVideoStream : IVideoStream, IDisposable
    {
        private readonly VideoCapture _video;
        private bool _disposed;

        
        public WebCamVideoStream(int inputStream) {
            var capType = VideoCapture.API.DShow;
            _video = new VideoCapture(inputStream, capType);
            var foo = _video.CaptureSource.ToString();
            Frame = new Mat();
            LogUtil.Write("Stream init, capture source is " + foo + ", " + inputStream);            
        }

        private void SetFrame(object sender, EventArgs e) {
            if (_video != null && _video.Ptr != IntPtr.Zero) {
                _video.Read(Frame);
            } else {
                LogUtil.Write("No frame to set...");
            }
        }
        
        public async Task Start(CancellationToken ct) {
            LogUtil.Write("WebCam Stream started.");
            _video.ImageGrabbed += SetFrame;
            _video.Start();
        }

        public Mat Frame { get; set; }

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