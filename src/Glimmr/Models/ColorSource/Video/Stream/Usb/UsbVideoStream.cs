#region

using System;
using System.Runtime.InteropServices;
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
			//_video?.Stop();
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

			if (CheckVideo()) {
				return Task.CompletedTask;
			}

			SetVideo();
			if (!CheckVideo()) {
				Log.Warning("Still unable to set video.");
			}


			return Task.CompletedTask;
		}

		private void SetVideo() {
			_video?.Dispose();
			var props = new Tuple<CapProp, int>[] {
				new(CapProp.FourCC,VideoWriter.Fourcc('M', 'J', 'P', 'G')),
				new(CapProp.Fps,60),
				new(CapProp.FrameWidth,640),
				new(CapProp.FrameHeight,480)
			};
			var api = OperatingSystem.IsWindows() ? VideoCapture.API.DShow : VideoCapture.API.V4L2;
			_video = new VideoCapture(_inputStream, api);
			foreach (var (prop, val) in props) {
				_video.SetCaptureProperty(prop, val);
			}
		}

		private bool CheckVideo() {
			if (_video == null) {
				Log.Warning("Unable to set video stream.");
				return false;
			}
			var fourCc = (int)_video.GetCaptureProperty(CapProp.FourCC);
			var fps = (int) _video.GetCaptureProperty(CapProp.Fps);
			var d5 = VideoWriter.Fourcc('M', 'J', 'P', 'G');

			Log.Debug($"Video created, fps and 4cc are {fps} and {fourCc} versus {d5}.");
			if (fps == 60 && fourCc == d5) {
				return true;
			} else {
				if (fps != 60) Log.Debug("FPS");
				if (fourCc != d5) Log.Debug("4cc");
			}

			Log.Warning("Video problems, bob...");
			_video?.Dispose();
			_video = null;
			return false;
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

		private void DisposeVideo() {
			Log.Debug("Disposing video.");
			try {
				if (_video != null) {
					var ptr = _video.Ptr;
					if (ptr != IntPtr.Zero) {
						Log.Debug("Releasing cap??");
						//cveVideoCaptureRelease(ref ptr);
						Log.Debug("Released");
					} else {
						Log.Debug("No pointer.");
					}
				} else {
					Log.Debug("Video is null");
				}

			} catch (Exception e) {
				Log.Warning("Exception: " + e.Message);
			}
			_video?.Dispose();
			_video = null;
		}

		protected virtual void Dispose(bool disposing) {
			if (!disposing) {
				return;
			}
			DisposeVideo();
		}
		
		[DllImport("cvextern", CallingConvention = CallingConvention.Cdecl)]
		private static extern void cveVideoCaptureRelease(ref IntPtr capture);
	}
}