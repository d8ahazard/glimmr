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
		private int _inputStream;


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
				await Task.Delay(TimeSpan.FromMilliseconds(1), CancellationToken.None);
			}
		}

		public Task Stop() {
			_video?.Stop();
			Dispose();
			return Task.CompletedTask;
		}

		public Task Refresh() {
			var sd = DataUtil.GetSystemData();
			var inputStream = sd.UsbSelection;
			if (inputStream != _inputStream || _video == null) {
				_inputStream = inputStream;
				SetVideo();
			}

			if (_video == null) {
				Log.Warning("Unable to set video stream.");
				return Task.CompletedTask;
			}
			var fourCc = _video.GetCaptureProperty(CapProp.FourCC);
			var fps = _video.GetCaptureProperty(CapProp.Fps);
			double d5 = VideoWriter.Fourcc('M', 'J', 'P', 'G');
			if (Math.Abs(fourCc - d5) > float.MinValue || Math.Abs(fps - 60) > float.MinValue) {
				Log.Information("Couldn't set fc or fps, re-creating.");
				SetVideo();
			}
			
			Log.Debug($"Video created, fps and 4cc are {fps} and {fourCc}.");
			//Log.Debug("API is " + _video.BackendName);
			//Log.Warning(!fourcc ? "FourCC not set." : "FourCC Set.");
			
			
			return Task.CompletedTask;
		}

		private void SetVideo() {
			_video?.Stop();
			_video?.Dispose();
			_video = OperatingSystem.IsWindows() ? new VideoCapture(_inputStream,VideoCapture.API.DShow) : new VideoCapture(_inputStream,VideoCapture.API.V4L2);
			_video.SetCaptureProperty(CapProp.FourCC, VideoWriter.Fourcc('M', 'J', 'P', 'G'));
			_video.SetCaptureProperty(CapProp.Fps, 60);
			_video.SetCaptureProperty(CapProp.FrameWidth, 640);
			_video.SetCaptureProperty(CapProp.FrameHeight, 480);
		}

		private void SetFrame(object? sender, EventArgs e) {
			if (_video != null && _video.Ptr != IntPtr.Zero) {
				using var frame = new Mat();
				_video.Read(frame);
				_splitter?.Update(frame);
			} else {
				if (_splitter != null) {
					_splitter.SourceActive = false;
				}

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