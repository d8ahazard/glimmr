using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Glimmr.Models.Util;
using Serilog;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;

namespace Glimmr.Models.ColorSource.Video.Stream.Screen {
	public class ScreenVideoStream : IVideoStream, IDisposable {
		public int Size { get; private set; }
		private Adapter _adapter;
		private Bitmap _bmpScreenCapture;
		private int _bottom;
		private bool _capturing;
		private Device _device;
		private bool _doSave;
		private int _height;
		private int _left;

		private byte[] _previousScreen;
		private int _right;
		private bool _run, _init;


		private Image<Bgr, byte> _screen;
		private Rectangle _screenDims;
		private Texture2D _screenTexture;
		private int _top;
		private int _width;

		public void Dispose() {
			_bmpScreenCapture?.Dispose();
			_screen?.Dispose();
			GC.SuppressFinalize(this);
		}

		public Mat Frame { get; set; }

		public Task Start(CancellationToken ct) {
			
			SetDimensions();
			_bmpScreenCapture = new Bitmap(_width, _height);
			
			if (_width == 0 || _height == 0) {
				Log.Debug("We have no screen, returning.");
				return Task.CompletedTask;
			}

			_doSave = true;

			Log.Debug("Starting screen capture, width is " + _width + " height is " + _height + ".");

			_capturing = true;
			return Task.Run(() => CaptureScreen(ct));
		}

		public Task Stop() {
			_capturing = false;
			Dispose();
			return Task.CompletedTask;
		}

		public Task Refresh() {
			SetDimensions();
			return Task.CompletedTask;
		}

		public Task SaveFrame() {
			_doSave = true;
			return Task.CompletedTask;
		}


		private void SetDimensions() {
			SystemData sd = DataUtil.GetSystemData();
			_screenDims = DisplayUtil.GetDisplaySize();
			var rect = _screenDims;
			if (!RectContains(_screenDims, rect)) {
				Log.Debug("Selected capture rect is outside of screen!");
				return;
			}

			_left = rect.Left;
			_top = rect.Top;
			_width = rect.Width;
			_height = rect.Height;
			

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
					using (var g = Graphics.FromImage(_bmpScreenCapture)) {
						g.CopyFromScreen(_left, _top, 0, 0, _bmpScreenCapture.Size, CopyPixelOperation.SourceCopy);
						_screen = _bmpScreenCapture.ToImage<Bgr, byte>();
						g.Flush();
					}

					var newMat = _screen.Resize(DisplayUtil.CaptureWidth, DisplayUtil.CaptureHeight, Inter.Nearest);
					Frame = newMat.Mat;
				}
			

			Log.Debug("Capture completed?");
		}
	}
}