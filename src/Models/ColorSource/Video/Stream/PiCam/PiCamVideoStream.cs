#region

using System;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.Structure;
using Glimmr.Models.Util;
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

namespace Glimmr.Models.ColorSource.Video.Stream.PiCam {
	public sealed class PiCamVideoStream : IVideoStream, IDisposable {
		private readonly MMALCamera _cam;
		private static readonly int CapHeight = 480;
		private static readonly int CapWidth = 640;
		public Mat Frame { get; set; }


		public PiCamVideoStream() {
			_cam = MMALCamera.Instance;
			Frame = new Mat();
		}

		
		public async Task Start(CancellationToken ct) {
			Log.Debug("Starting Camera...");
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
			vidCaptureHandler.MyEmguEvent += OnEmguEventCallback;

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

			// Camera warm up time
			Log.Debug("Camera is warming up...");
			await Task.Delay(2000,ct);
			Log.Debug("Camera initialized.");
			await _cam.ProcessAsync(_cam.Camera.VideoPort, ct).ConfigureAwait(false);
			Log.Debug("Camera closed.");
		}

		public Task Stop() {
			Dispose();
			return Task.CompletedTask;
		}

		public Task Refresh() {
			return Task.CompletedTask;
		}

		private void OnEmguEventCallback(object sender, EmguEventArgs args) {
			var input = new Image<Bgr, byte>(CapWidth, CapHeight);
			input.Bytes = args.ImageData;
			Frame = input.Mat;
			input.Dispose();
		}

		private class EmguEventArgs : EventArgs {
			public byte[] ImageData { get; set; }
		}

		private class EmguInMemoryCaptureHandler : InMemoryCaptureHandler, IVideoCaptureHandler {
			public override void Process(ImageContext context) {
				base.Process(context);

				if (context != null && context.Eos) {
					MyEmguEvent?.Invoke(this, new EmguEventArgs {ImageData = WorkingData.ToArray()});
					WorkingData.Clear();
				}
			}

			public void Split() {
			}

			public event EventHandler<EmguEventArgs> MyEmguEvent;
		}

		#region IDisposable Support

		private bool disposedValue;


		private void Dispose(bool disposing) {
			if (disposedValue) {
				return;
			}

			if (disposing) {
				_cam.Cleanup();
			}

			disposedValue = true;
		}

		public void Dispose() {
			Dispose(true);
		}

		#endregion
	}
}