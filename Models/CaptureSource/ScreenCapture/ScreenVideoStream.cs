using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.Structure;
using HueDream.Models.Util;

namespace HueDream.Models.CaptureSource.ScreenCapture {
    public class ScreenVideoStream : IVideoStream {
        public Mat Frame { get; set; }

        Mat IVideoStream.Frame {
            get => Frame;
            set => Frame = value;
        }

        public Task Start(CancellationToken ct) {
            LogUtil.Write("Starting screen capture??");
            while (!ct.IsCancellationRequested) {
                CaptureScreen();
            }
            LogUtil.Write("Screen capture complete!");
            return Task.CompletedTask;
        }
        
        private void CaptureScreen() {
            var s = DisplayUtil.GetDisplaySize();
            var width = s.Width;
            var height = s.Height;
            Bitmap bmpScreenCapture = new Bitmap(width, height);
            Graphics g = Graphics.FromImage(bmpScreenCapture);
            g.CopyFromScreen(0, 0, 0, 0, s, CopyPixelOperation.SourceCopy);
            Frame = bmpScreenCapture.ToImage<Bgr, Byte>().Mat;
            bmpScreenCapture.Dispose();
        }
    }
}