using System;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using HueDream.Models.Util;

namespace HueDream.Models.Capture {
    public class WebCamVideoStream : IVideoStream, IDisposable
    {

        private readonly VideoCapture video;
        private readonly Mat frame;
        private bool disposed;

        Mat IVideoStream.Frame => frame;

        public WebCamVideoStream(int inputStream) {
            video = new VideoCapture(inputStream, VideoCapture.API.DShow);
            frame = new Mat();
            LogUtil.Write("Stream init.");            
        }
       

        private void SetFrame(object sender, EventArgs e) {
            if (video != null && video.Ptr != IntPtr.Zero) {
                video.Read(frame);
            }
        }

       



        public async Task Start(CancellationToken ct) {
            LogUtil.Write("WebCam Stream started.");
            video.ImageGrabbed += SetFrame;
            video.Start();
        }

        public void Dispose() {
            if (disposed) return;
            disposed = true;
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposing) return;
            frame.Dispose();
            video.Dispose();
        }
    }
}