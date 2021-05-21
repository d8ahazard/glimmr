using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Glimmr.Models.Util;
using Newtonsoft.Json;
using Serilog;

namespace Glimmr.Models.ColorSource.Video.Stream.Screen {
	public class ScreenVideoStream : IVideoStream, IDisposable {
		private bool _capturing;
		private bool _doSave;
		private int _height;
		private int _left;


		private Rectangle _screenDims;
		private int _top;
		private int _width;

		public void Dispose() {
			GC.SuppressFinalize(this);
		}

		public Mat Frame { get; set; }

		public Task Start(CancellationToken ct) {
			try {
				SetDimensions();

				if (_width == 0 || _height == 0) {
					Log.Debug("We have no screen, returning.");
					return Task.CompletedTask;
				}

				_doSave = true;

				Log.Debug("Starting screen capture, width is " + _width + " height is " + _height + ".");

				_capturing = true;
				return Task.Run(() => CaptureScreen(ct));
			} catch (Exception e) {
				Log.Warning("Exception, can't start screen cap: " + e.Message);
				_capturing = false;
				return Task.CompletedTask;
			}
		}

		public Task Stop() {
			_capturing = false;
			Dispose();
			return Task.CompletedTask;
		}

		public Task Refresh() {
			Log.Debug("Refreshing...");
			SetDimensions();
			return Task.CompletedTask;
		}

		public Task SaveFrame() {
			_doSave = true;
			return Task.CompletedTask;
		}


		private void SetDimensions() {
			_screenDims = DisplayUtil.GetDisplaySize();
			var rect = _screenDims;
			_width = 0;
			_height = 0;
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
			Log.Debug("Screen capture dimensions set: " + JsonConvert.SerializeObject(rect));
		}

		private static bool RectContains(Rectangle outer, Rectangle inner) {
			return outer.Left <= inner.Left && outer.Right >= inner.Right && outer.Top <= inner.Top &&
			       outer.Bottom >= inner.Bottom;
		}

		private void CaptureScreen(CancellationToken ct) {
			Log.Debug("Screen capture started...");

			while (!ct.IsCancellationRequested && _capturing) {
				var bcs = new Bitmap(_width, _height);
				using (var g = Graphics.FromImage(bcs)) {
					g.CopyFromScreen(_left, _top, 0, 0, bcs.Size, CopyPixelOperation.SourceCopy);
					var sc = bcs.ToImage<Bgr, byte>();
					g.Flush();
					var newMat = sc.Resize(DisplayUtil.CaptureWidth, DisplayUtil.CaptureHeight, Inter.Nearest);
					Frame = newMat.Mat;
				}
			}

			Log.Debug("Capture completed?");
		}
	}
}