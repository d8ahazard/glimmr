using System;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Glimmr.Models.Util;

namespace Glimmr.Models.CaptureSource.Video.WebCam {
    public class WebCamVideoStream : IVideoStream, IDisposable
    {
        private readonly VideoCapture _video;
        public Mat Frame;
        private bool _disposed;

        Mat IVideoStream.Frame {
            get => Frame;
            set => Frame = value;
        }

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