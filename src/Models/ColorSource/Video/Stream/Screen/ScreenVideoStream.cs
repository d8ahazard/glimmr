using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Glimmr.Models.Util;
using Newtonsoft.Json;
using Serilog;

namespace Glimmr.Models.ColorSource.Video.Stream.Screen {
    public class ScreenVideoStream : IVideoStream, IDisposable
    {
        public Mat Frame { get; set; }

        
        private Image<Bgr, byte> _screen;
        private Bitmap _bmpScreenCapture;
        private int _width;
        private int _height;
        private int _left;
        private int _top;
        private int _bottom;
        private int _right;

        public Task Start(CancellationToken ct) {
            var s = DisplayUtil.GetDisplaySize();
            var d = DisplayUtil.GetMonitorInfo();
            Log.Debug("Display adapters: " + JsonConvert.SerializeObject(d));
            _width = s.Width;
            _height = s.Height;
            if (_width == 0 || _height == 0) {
                Log.Debug("We have no screen, returning.");
                return Task.CompletedTask;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                // Enumerate system display devices
                var devIdx = 0;
                while (true) {
                    var deviceData = new DisplayUtil.DisplayDevice{cb = Marshal.SizeOf(typeof(DisplayUtil.DisplayDevice))};
                    if (DisplayUtil.EnumDisplayDevices(null, devIdx, ref deviceData, 0) != 0) {
                        // Get the position and size of this particular display device
                        var devMode = new DisplayUtil.DEVMODE();
                        if (DisplayUtil.EnumDisplaySettings(deviceData.DeviceName, DisplayUtil.ENUM_CURRENT_SETTINGS, ref devMode)) {
                            Log.Debug("Adding device: " + JsonConvert.SerializeObject(deviceData));
                            // Update the virtual screen dimensions
                            _left = Math.Min(_left, devMode.dmPositionX);
                            _top = Math.Min(_top, devMode.dmPositionY);
                            _right = Math.Max(_right, devMode.dmPositionX + devMode.dmPelsWidth);
                            _bottom = Math.Max(_bottom, devMode.dmPositionY + devMode.dmPelsHeight);
                            _width = _left - _right;
                            _height = _top - _bottom;
                        }
                        devIdx++;
                    }
                    else
                        break;
                }
            }

            _width = Math.Abs(_width);
            _height = Math.Abs(_height);
            Log.Debug("Starting screen capture, width is " + _width + " height is " + _height + ".");
            _bmpScreenCapture = new Bitmap(_width, _height);
            return Task.Run(() => CaptureScreen(ct));
        }
        
        private void CaptureScreen(CancellationToken ct) {
            Log.Debug("Screen capture started...");
            while (!ct.IsCancellationRequested) {
                var g = Graphics.FromImage(_bmpScreenCapture);
                g.CopyFromScreen(_left, _top, 0, 0, _bmpScreenCapture.Size, CopyPixelOperation.SourceCopy);
                _screen = _bmpScreenCapture.ToImage<Bgr, byte>();
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