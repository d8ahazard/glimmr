#region

using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Models.ColorSource.Video.Stream;
using Glimmr.Models.ColorSource.Video.Stream.PiCam;
using Glimmr.Models.ColorSource.Video.Stream.Screen;
using Glimmr.Models.ColorSource.Video.Stream.Usb;
using Glimmr.Models.Util;
using Glimmr.Services;
using Microsoft.Extensions.Hosting;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.Video {
	public sealed class VideoStream : BackgroundService, IColorSource {
		// should we send them to devices?
		public bool SendColors {
			set => StreamSplitter.DoSend = value;
		}

		public FrameSplitter StreamSplitter { get; }


		// Loaded data
		private CameraType _camType;
		private CaptureMode _captureMode;

		private SystemData _systemData;


		// Video source and splitter
		private IVideoStream? _vc;


		public VideoStream(ColorService colorService) {
			_systemData = DataUtil.GetSystemData();
			colorService.ControlService.RefreshSystemEvent += RefreshSystem;
			StreamSplitter = new FrameSplitter(colorService, true);
		}

		public Task ToggleStream(CancellationToken ct) {
			Log.Debug("Starting video stream service...");
			SendColors = true;
			StreamSplitter.DoSend = true;
			return ExecuteAsync(ct);
		}

		public bool SourceActive => StreamSplitter.SourceActive;

		public void RefreshSystem() {
			_systemData = DataUtil.GetSystemData();
			_captureMode = (CaptureMode) _systemData.CaptureMode;
			_camType = (CameraType) _systemData.CamType;
		}

		public Color[] GetColors() {
			return StreamSplitter.GetColors();
		}

		public Color[] GetSectors() {
			return StreamSplitter.GetSectors();
		}

		protected override Task ExecuteAsync(CancellationToken ct) {
			return Task.Run(async () => {
				SetCapVars();
				_systemData = DataUtil.GetSystemData();
				_captureMode = (CaptureMode) _systemData.CaptureMode;
				_camType = (CameraType) _systemData.CamType;
				_vc = GetStream();
				if (_vc == null) {
					Log.Information("We have no video source, returning.");
					return;
				}

				await _vc.Start(ct, StreamSplitter);
				while (!ct.IsCancellationRequested) {
					await Task.Delay(10, CancellationToken.None);
					Log.Debug("SA: " + SourceActive);
				}

				await _vc.Stop();
				SendColors = false;
				Log.Information("Video stream service stopped.");
			}, CancellationToken.None);
		}


		private void SetCapVars() {
			_systemData = DataUtil.GetSystemData();
		}

		private IVideoStream? GetStream() {
			switch (_captureMode) {
				case CaptureMode.Camera:
					switch (_camType) {
						case CameraType.RasPiCam:
							// 0 = pi module, 1 = web cam
							Log.Information("Using Pi cam for capture.");
							return new PiCamVideoStream();
						case CameraType.WebCam:
							Log.Information("Using web cam for capture.");
							return new UsbVideoStream();
					}

					return null;
				case CaptureMode.Hdmi:
					Log.Information("Using usb stream for capture.");
					return new UsbVideoStream();

				case CaptureMode.Screen:
					Log.Information("Using screen for capture.");
					return new ScreenVideoStream();
			}

			return null;
		}
	}
}