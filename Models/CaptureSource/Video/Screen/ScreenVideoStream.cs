using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using HueDream.Models.Util;

namespace HueDream.Models.CaptureSource.Video.Screen {
    public class ScreenVideoStream : IVideoStream, IDisposable
    {
        public Mat Frame { get; set; }

        Mat IVideoStream.Frame {
            get => Frame;
            set => Frame = value;
        }

        private Image<Bgr, byte> _screen;
        private Bitmap _bmpScreenCapture;

        public Task Start(CancellationToken ct) {
            var s = DisplayUtil.GetDisplaySize();
            var width = s.Width;
            var height = s.Height;
            if (width == 0 || height == 0) {
                LogUtil.Write("We have no screen, returning.");
                return Task.CompletedTask;
            }
            LogUtil.Write("Starting screen capture, width is " + width + " height is " + height + ".");
            _bmpScreenCapture = new Bitmap(width, height);
            return Task.Run(() => CaptureScreen(s, ct));
        }
        
        private void CaptureScreen(Size s, CancellationToken ct) {
        
            while (!ct.IsCancellationRequested) {
                Graphics g = Graphics.FromImage(_bmpScreenCapture);
                g.CopyFromScreen(0, 0, 0, 0, s, CopyPixelOperation.SourceCopy);
                _screen = _bmpScreenCapture.ToImage<Bgr, Byte>();
                var newMat = _screen.Resize(600, 400, Inter.Nearest);
                Frame = newMat.Mat;
            }
            LogUtil.Write("Capture completed?");
        }

        public void Dispose() {
            _bmpScreenCapture?.Dispose();
            _screen?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}