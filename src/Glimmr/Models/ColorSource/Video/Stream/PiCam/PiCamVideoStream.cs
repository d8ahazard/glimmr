#region

using System;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.Structure;
using Glimmr.Models.Frame;
using MMALSharp;
using MMALSharp.Common;
using MMALSharp.Common.Utility;
using MMALSharp.Components;
using MMALSharp.Handlers;
using MMALSharp.Native;
using MMALSharp.Ports;
using MMALSharp.Ports.Outputs;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.Video.Stream.PiCam;

public sealed class PiCamVideoStream : IVideoStream, IDisposable {
	private const int CapHeight = 480;
	private const int CapWidth = 640;
	private readonly MMALCamera _cam;
	private FrameSplitter? _splitter;

	public PiCamVideoStream() {
		_cam = MMALCamera.Instance;
	}


	public async Task Start(FrameSplitter frameSplitter, CancellationToken ct) {
		_splitter = frameSplitter;
		MMALCameraConfig.VideoStabilisation = false;

		MMALCameraConfig.SensorMode = MMALSensorMode.Mode1;
		MMALCameraConfig.ExposureMode = MMAL_PARAM_EXPOSUREMODE_T.MMAL_PARAM_EXPOSUREMODE_BACKLIGHT;
		MMALCameraConfig.VideoResolution = new Resolution(CapWidth, CapHeight);
		MMALCameraConfig.VideoFramerate = new MMAL_RATIONAL_T(60, 1);

		using var vidCaptureHandler = new EmguInMemoryCaptureHandler();
		using var splitter = new MMALSplitterComponent();
		using var renderer = new MMALNullSinkComponent();
		_cam.ConfigureCameraSettings();
		Log.Debug("Cam mode is " + MMALCameraConfig.SensorMode);
		// Register to the event.
		vidCaptureHandler.MyEmguEvent += ProcessFrame;

		// We are instructing the splitter to do a format conversion to BGR24.
		var splitterPortConfig =
			new MMALPortConfig(MMALEncoding.BGR24, MMALEncoding.BGR24, CapWidth, CapHeight, null);

		// By default in MMALSharp, the Video port outputs using proprietary communication (Opaque) with a YUV420 pixel format.
		// Changes to this are done via MMALCameraConfig.VideoEncoding and MMALCameraConfig.VideoSub format.                
		splitter.ConfigureInputPort(
			new MMALPortConfig(MMALEncoding.OPAQUE, MMALEncoding.I420, CapWidth, CapHeight, null),
			_cam.Camera.VideoPort, null);

		// We then use the splitter config object we constructed earlier. We then tell this output port to use our capture handler to record data.
		splitter.ConfigureOutputPort<SplitterVideoPort>(0, splitterPortConfig, vidCaptureHandler);

		_cam.Camera.PreviewPort.ConnectTo(renderer);
		_cam.Camera.VideoPort.ConnectTo(splitter);

		await Task.Delay(2000, ct);
		await _cam.ProcessAsync(_cam.Camera.VideoPort, ct).ConfigureAwait(false);
		Log.Debug("Camera closed.");
	}

	public Task Stop() {
		Dispose();
		return Task.CompletedTask;
	}

	private void ProcessFrame(object? sender, EmguEventArgs args) {
		var input = new Image<Bgr, byte>(CapWidth, CapHeight) { Bytes = args.ImageData };
		_splitter?.Update(input.Mat.Clone());
		input.Dispose();
	}

	private class EmguEventArgs : EventArgs {
		public byte[]? ImageData { get; init; }
	}

	private class EmguInMemoryCaptureHandler : InMemoryCaptureHandler, IVideoCaptureHandler {
		public override void Process(ImageContext context) {
			base.Process(context);

			if (context is not { Eos: true }) {
				return;
			}

			MyEmguEvent?.Invoke(this, new EmguEventArgs { ImageData = WorkingData.ToArray() });
			WorkingData.Clear();
		}

		public void Split() {
		}

		public event EventHandler<EmguEventArgs>? MyEmguEvent;
	}

	#region IDisposable Support

	private bool _disposedValue;


	private void Dispose(bool disposing) {
		if (_disposedValue) {
			return;
		}

		if (disposing) {
			_cam.Cleanup();
		}

		_disposedValue = true;
	}

	public void Dispose() {
		Dispose(true);
	}

	#endregion
}