using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.Structure;
using Pranas;

namespace HueDream.Models.CaptureSource.Screen {
    public class ScreenVideoStream : IVideoStream {
        public Mat Frame { get; set; }

        Mat IVideoStream.Frame {
            get => Frame;
            set => Frame = value;
        }

        public Task Start(CancellationToken ct) {
            while (!ct.IsCancellationRequested) {
                var screen = ScreenshotCapture.TakeScreenshot();
                Frame = GetMatFromSdImage(screen);
            }

            return Task.CompletedTask;
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