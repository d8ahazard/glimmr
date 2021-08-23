#region

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.Cuda;
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
		

		public UsbVideoStream() {
			//Refresh();
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
				_video?.Stop();
				_video?.Dispose();
				if (OperatingSystem.IsWindows()) {
					_video = new VideoCapture(inputStream,VideoCapture.API.DShow);	
				} else {
					_video = new VideoCapture(inputStream,VideoCapture.API.V4L2);
				}
				
				_inputStream = inputStream;
			}

			if (_video == null) return Task.CompletedTask;
			_video.SetCaptureProperty(CapProp.FourCC, VideoWriter.Fourcc('M', 'J', 'P', 'G'));
			_video.SetCaptureProperty(CapProp.Fps, 60);
			var fourCC = _video.GetCaptureProperty(CapProp.FourCC);
			double d4 = VideoWriter.Fourcc('Y', 'U', 'Y', 'V');
			double d5 = VideoWriter.Fourcc('M', 'J', 'P', 'G');
			var fps = _video.GetCaptureProperty(CapProp.Fps);
			Log.Debug($"Video created, fps and 4cc are {fps} and {fourCC} vs {d4} or {d5}.");
			//Log.Debug("API is " + _video.BackendName);
			//Log.Warning(!fourcc ? "FourCC not set." : "FourCC Set.");
			_video.SetCaptureProperty(CapProp.FrameWidth, 640);
			_video.SetCaptureProperty(CapProp.FrameHeight, 480);
			
			return Task.CompletedTask;
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