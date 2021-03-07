using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Glimmr.Models.Util;
using Serilog;

namespace Glimmr.Models.ColorSource.Video.Stream.Screen {
    public class ScreenVideoStream : IVideoStream, IDisposable {
        public Mat Frame { get; set; }

        
        private Image<Bgr, byte> _screen;
        private Bitmap _bmpScreenCapture;
        private int _width;
        private int _height;
        private int _left;
        private int _top;
        private int _bottom;
        private int _right;
        private bool _capturing;
        private bool _doSave;

        public Task Start(CancellationToken ct) {
            SetDimensions();
            
            if (_width == 0 || _height == 0) {
                Log.Debug("We have no screen, returning.");
                return Task.CompletedTask;
            }

            _doSave = true;
            Log.Debug("Starting screen capture, width is " + _width + " height is " + _height + ".");
            _bmpScreenCapture = new Bitmap(_width, _height);
            _capturing = true;
            return Task.Run(() => CaptureScreen(ct));
        }
        
        public Task Stop() {
            _capturing = false;
            Dispose();
            return Task.CompletedTask;
        }

        private void SetDimensions() {
            SystemData sd = DataUtil.GetObject<SystemData>("SystemData");
            var mode = sd.ScreenCapMode;
            var screenSize = DisplayUtil.GetDisplaySize();
            if (mode == 0) {
                var rect = sd.CaptureRegion;
                if (!RectContains(screenSize, rect)) {
                    Log.Debug("Selected capture rect is outside of screen!");
                    return;
                }
                
                _left = rect.Left;
                _top = rect.Top;
                _width = rect.Width;
                _height = rect.Height;
            } else {
                var monitors = DataUtil.GetCollection<MonitorInfo>("Dev_Video");
                if (monitors.Count == 0) {
                    Log.Debug("No monitors are selected!");
                }
                
                foreach (var monitor in monitors.Where(monitor => monitor.Enable)) {
                    _left = Math.Min(_left, monitor.DmPositionX);
                    _top = Math.Min(_top, monitor.DmPositionY);
                    _right = Math.Max(_right, monitor.DmPositionX + monitor.DmPelsWidth);
                    _bottom = Math.Max(_bottom, monitor.DmPositionY + monitor.DmPelsHeight);
                    _width = _left - _right;
                    _height = _top - _bottom;
                }
            }
            
            _width = Math.Abs(_width);
            _height = Math.Abs(_height);
        }

        private static bool RectContains(Rectangle outer, Rectangle inner) {
            return outer.Left <= inner.Left && outer.Right >= inner.Right && outer.Top <= inner.Top &&
                   outer.Bottom >= inner.Bottom;
        }
        
        private void CaptureScreen(CancellationToken ct) {
            Log.Debug("Screen capture started...");
            while (!ct.IsCancellationRequested && _capturing) {
                var g = Graphics.FromImage(_bmpScreenCapture);
                g.CopyFromScreen(_left, _top, 0, 0, _bmpScreenCapture.Size, CopyPixelOperation.SourceCopy);
                _screen = _bmpScreenCapture.ToImage<Bgr, byte>();
                if (_doSave) {
                    var path = Directory.GetCurrentDirectory();
                    var fullPath = Path.Join(path, "wwwroot", "img", "_preview_screen.jpg");
                    var gMat = new Mat();
                    _screen.Mat.CopyTo(gMat);
                    gMat.Save(fullPath);
                    gMat.Dispose();
                }
                var newMat = _screen.Resize(DisplayUtil.CaptureWidth, DisplayUtil.CaptureHeight, Inter.Nearest);
                Frame = newMat.Mat;
            }
            Log.Debug("Capture completed?");
        }

        public void Dispose() {
            _bmpScreenCapture?.Dispose();
            _screen?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}