using System;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using HueDream.Models.Util;

namespace HueDream.Models.DreamGrab {
    public class WebCamVideoStream : IVideoStream {

        private VideoCapture video;
        private Mat frame;

        public WebCamVideoStream(int inputStream) {
            video = new VideoCapture(inputStream);
            frame = new Mat();
            LogUtil.Write("Stream init.");            
        }

        public Mat GetFrame() {
            LogUtil.Write("Frame got?");
            return frame;
        }

        private void SetFrame(object sender, EventArgs e) {
            if (video != null && video.Ptr != IntPtr.Zero) {
                LogUtil.Write("Frame saved.");
                video.Read(frame);
                CvInvoke.Imshow("Frame", frame);
            }
        }

       



        public async Task Start(CancellationToken ct) {
            LogUtil.Write("WebCam Stream started.");
            video.ImageGrabbed += SetFrame;
            video.Start();
        }
    }
}