using System;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using HueDream.Models.Util;

namespace HueDream.Models.CaptureSource.Camera {
    public class WebCamVideoStream : IVideoStream, IDisposable
    {

        private readonly VideoCapture video;
        public Mat Frame;
        private bool disposed;

        Mat IVideoStream.Frame {
            get => Frame;
            set => Frame = value;
        }

        public WebCamVideoStream(int inputStream) {
            video = new VideoCapture(inputStream, VideoCapture.API.DShow);
            Frame = new Mat();
            LogUtil.Write("Stream init.");            
        }
       

        private void SetFrame(object sender, EventArgs e) {
            if (video != null && video.Ptr != IntPtr.Zero) {
                video.Read(Frame);
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
            Frame.Dispose();
            video.Dispose();
        }
    }
}