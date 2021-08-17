#region

using System;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Glimmr.Models.Util;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.Video.Stream.Usb {
	public class UsbVideoStream : IVideoStream, IDisposable {
		private bool _disposed;
		private FrameSplitter? _splitter;
		private VideoCapture? _video;
		

		public UsbVideoStream() {
			Refresh();
		}

		public void Dispose() {
			if (_disposed) {
				return;
			}

			_disposed = true;
			GC.SuppressFinalize(this);
			Dispose(true);
		}

		public async Task Start(CancellationToken ct, FrameSplitter splitter) {
			Log.Debug("Starting USB Stream...");
			_splitter = splitter;
			await Refresh();
			if (_video == null) {
				return;
			}

			_video.ImageGrabbed += SetFrame;
			_video.Start();
			Log.Debug("USB Stream started.");
			while (!ct.IsCancellationRequested) {
				await Task.Delay(TimeSpan.FromMilliseconds(16), CancellationToken.None);
			}
		}

		public Task Stop() {
			_video?.Stop();
			Dispose();
			return Task.CompletedTask;
		}

		public Task Refresh() {
			_video?.Stop();
			_video?.Dispose();
			var sd = DataUtil.GetSystemData();
			var inputStream = sd.UsbSelection;
			_video = new VideoCapture(inputStream);
			//_video.SetCaptureProperty(CapProp.FourCC, VideoWriter.Fourcc('M', 'J', 'P', 'G'));
			_video.SetCaptureProperty(CapProp.FrameWidth, 640);
			_video.SetCaptureProperty(CapProp.FrameHeight, 480);
			//_video.SetCaptureProperty(CapProp.Fps, 30);
			return Task.CompletedTask;
		}

		private void SetFrame(object? sender, EventArgs e) {
			if (_video != null && _video.Ptr != IntPtr.Zero) {
				using var frame = new Mat();
				_video.Read(frame);
				_splitter?.Update(frame);
			} else {
				Log.Debug("No frame to set...");
			}
		}


		protected virtual void Dispose(bool disposing) {
			if (!disposing) {
				return;
			}

			_video?.Dispose();
		}
	}
}