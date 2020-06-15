using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.Structure;
using HueDream.Models.Util;
using Newtonsoft.Json;

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
            using Graphics g = Graphics.FromImage(bmpScreenCapture);
            g.CopyFromScreen(0, 0,0, 0, bmpScreenCapture.Size, CopyPixelOperation.SourceCopy);
            //var foo = bmpScreenCapture.ToImage(Bgr, Byte);
            //var foo2 = bmpScreenCapture.ToMat();
            //Frame = imageCV.Mat;
        }


        private Mat GetMatFromSdImage(Image image) {
            int stride;
            var bmp = new Bitmap(image);

            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var bmpData = bmp.LockBits(rect, ImageLockMode.ReadWrite, bmp.PixelFormat);

            var pf = bmp.PixelFormat;
            if (pf == PixelFormat.Format32bppArgb) {
                stride = bmp.Width * 4;
            } else {
                stride = bmp.Width * 3;
            }

            var cvImage = new Image<Bgra, byte>(bmp.Width, bmp.Height, stride, bmpData.Scan0);

            bmp.UnlockBits(bmpData);
            var output = cvImage.Mat;
            cvImage.Dispose();
            bmp.Dispose();
            return output;
        }
    }
}