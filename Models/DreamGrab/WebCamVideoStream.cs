using System;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using HueDream.Models.Util;

namespace HueDream.Models.DreamGrab {
    public class WebCamVideoStream : IVideoStream {

        private VideoCapture video;
        private Mat frame;
        private bool saved;

        Mat IVideoStream.Frame => frame;

        public WebCamVideoStream(int inputStream) {
            video = new VideoCapture(inputStream, VideoCapture.API.DShow);
            frame = new Mat();
            saved = false;
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
    }
}