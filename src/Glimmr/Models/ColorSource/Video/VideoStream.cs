#region

using System;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Models.ColorSource.Video.Stream;
using Glimmr.Models.ColorSource.Video.Stream.PiCam;
using Glimmr.Models.ColorSource.Video.Stream.Screen;
using Glimmr.Models.ColorSource.Video.Stream.Usb;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.Video;

public class VideoStream : ColorSource {
	// should we send them to devices?
	public bool SendColors {
		set => FrameSplitter.DoSend = value;
	}

	public override bool SourceActive => FrameSplitter.SourceActive;

	public FrameSplitter FrameSplitter { get; }


	// Loaded data
	private CameraType _camType;
	private CaptureMode _captureMode;

	private SystemData _systemData;


	// Video source and splitter
	private IVideoStream? _vc;


	public VideoStream(ColorService colorService) {
		_systemData = DataUtil.GetSystemData();
		colorService.ControlService.RefreshSystemEvent += RefreshSystem;
		FrameSplitter = new FrameSplitter(colorService, true);
		SendColors = true;
	}

	public override Task Start(CancellationToken ct) {
		Log.Debug("Enabling video stream service...");
		RunTask = ExecuteAsync(ct);
		return Task.CompletedTask;
	}

	public override void RefreshSystem() {
		_systemData = DataUtil.GetSystemData();
		_captureMode = _systemData.CaptureMode;
		_camType = _systemData.CamType;
	}


	protected override Task ExecuteAsync(CancellationToken ct) {
		return Task.Run(async () => {
			SetCapVars();
			_systemData = DataUtil.GetSystemData();
			_captureMode = _systemData.CaptureMode;
			_camType = _systemData.CamType;
			_vc = GetStream();
			if (_vc == null) {
				Log.Information("We have no video source, returning.");
				return;
			}

			await _vc.Start(FrameSplitter, ct);
			while (!ct.IsCancellationRequested) {
				await Task.Delay(10, CancellationToken.None);
			}

			await _vc.Stop();
			Log.Information("Video stream service stopped.");
		}, CancellationToken.None);
	}


	private void SetCapVars() {
		_systemData = DataUtil.GetSystemData();
	}

	private IVideoStream GetStream() {
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
					default:
						throw new ArgumentOutOfRangeException();
				}

			case CaptureMode.Hdmi:
				Log.Information("Using usb stream for capture.");
				return new UsbVideoStream();

			case CaptureMode.Screen:
				Log.Information("Using screen for capture.");
				return new ScreenVideoStream();
			default:
				throw new ArgumentOutOfRangeException();
		}
	}
}