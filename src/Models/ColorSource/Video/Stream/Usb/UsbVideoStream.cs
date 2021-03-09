using System;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Glimmr.Models.Util;
using Serilog;

namespace Glimmr.Models.ColorSource.Video.Stream.Usb {
    public class UsbVideoStream : IVideoStream, IDisposable
    {
        private VideoCapture _video;
        private bool _disposed;
        private bool _doSave;

        
        public UsbVideoStream() {
            Frame = new Mat();
            Refresh();
        }

        private void SetFrame(object sender, EventArgs e) {
            if (_video != null && _video.Ptr != IntPtr.Zero) {
                _video.Read(Frame);
            } else {
                Log.Debug("No frame to set...");
            }
        }
        
        public async Task Start(CancellationToken ct) {
            Log.Debug("Starting USB Stream...");
            _video.ImageGrabbed += SetFrame;
            _video.Start();
            await Task.FromResult(true);
            Log.Debug("USB Stream started.");
        }

        public Task Stop() {
            _video.Stop();
            Dispose();
            return Task.CompletedTask;
        }

        public Task Refresh() {
            _video?.Stop();
            _video?.Dispose();
            SystemData sd = DataUtil.GetObject<SystemData>("SystemData");
            var devs = SystemUtil.ListUsb();
            var inputStream = sd.UsbSelection;
            _video = new VideoCapture(inputStream);
            _video.SetCaptureProperty(CapProp.FrameWidth, DisplayUtil.CaptureWidth);
            _video.SetCaptureProperty(CapProp.FrameHeight, DisplayUtil.CaptureHeight);
            var srcName = _video.CaptureSource.ToString();
            if (devs.ContainsKey(inputStream)) srcName = devs[inputStream];
            Log.Debug("Stream init, capture source is " + srcName + ", " + inputStream);
            _doSave = false;
            return Task.CompletedTask;
        }

        public Task SaveFrame() {
            _doSave = true;
            return Task.CompletedTask;
        }

        public Mat Frame { get; set; }

        public void Dispose() {
            if (_disposed) return;
            _disposed = true;
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposing) return;
            Frame.Dispose();
            _video.Dispose();
        }
    }
}