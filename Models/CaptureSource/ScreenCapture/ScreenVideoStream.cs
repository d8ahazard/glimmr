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

        private Image<Bgr, byte> _screen;
        private Bitmap _bmpScreenCapture;

        public Task Start(CancellationToken ct) {
            var s = DisplayUtil.GetDisplaySize();
            var width = s.Width;
            var height = s.Height;
            LogUtil.Write("Starting screen capture, width is " + width + " height is " + height + ".");
            _bmpScreenCapture = new Bitmap(width, height);
            return Task.Run(() => CaptureScreen(s, ct));
        }
        
        private void CaptureScreen(Size s, CancellationToken ct) {
            while (!ct.IsCancellationRequested) {
                Graphics g = Graphics.FromImage(_bmpScreenCapture);
                g.CopyFromScreen(0, 0, 0, 0, s, CopyPixelOperation.SourceCopy);
                _screen = _bmpScreenCapture.ToImage<Bgr, Byte>();
                Frame = _screen.Mat;
            }
            LogUtil.Write("Capture completed?");
        }
    }
}