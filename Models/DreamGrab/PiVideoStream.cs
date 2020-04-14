using System;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.Structure;
using HueDream.Models.Util;
using MMALSharp;
using MMALSharp.Common.Utility;
using MMALSharp.Components;
using MMALSharp.Handlers;
using MMALSharp.Native;
using MMALSharp.Ports;
using MMALSharp.Ports.Outputs;

namespace HueDream.Models.DreamGrab {
    public class PiVideoStream : IVideoStream, IDisposable {
        private MMALCamera cam;
        public Mat Frame;
        private int capWidth;
        private int capHeight;
        private int camMode;
        public PiVideoStream(int width = 1296, int height = 972, int mode = 4) {
            cam = MMALCamera.Instance;
            capWidth = width;
            capHeight = height;
            camMode = mode;
        }
       

        public class EmguEventArgs : EventArgs {
            public byte[] ImageData { get; set; }
        }

        public class EmguInMemoryCaptureHandler : InMemoryCaptureHandler, IVideoCaptureHandler {
            public event EventHandler<EmguEventArgs> MyEmguEvent;

            public override void Process(byte[] data, bool eos) {
                
                base.Process(data, eos);

                if (eos) {
                    this.MyEmguEvent(this, new EmguEventArgs { ImageData = this.WorkingData.ToArray() });
                    this.WorkingData.Clear();
                }
            }

            public void Split() {
                throw new NotImplementedException();
            }
        }

        public async Task Start(CancellationToken ct) {
            LogUtil.Write("Starting Camera...");

            MMALCamera cam = MMALCamera.Instance;
            MMALCameraConfig.VideoStabilisation = false;
                
            var sensorMode = MMALSensorMode.Mode0;
            switch(camMode) {
                case 1:
                    sensorMode = MMALSensorMode.Mode1;
                    break;
                case 2:
                    sensorMode = MMALSensorMode.Mode2;
                    break;
                case 3:
                    sensorMode = MMALSensorMode.Mode3;
                    break;
                case 4:
                    sensorMode = MMALSensorMode.Mode4;
                    break;
                case 5:
                    sensorMode = MMALSensorMode.Mode5;
                    break;
                case 6:
                    sensorMode = MMALSensorMode.Mode6;
                    break;
                case 7:
                    sensorMode = MMALSensorMode.Mode7;
                    break;
            }
            MMALCameraConfig.SensorMode = sensorMode;
            MMALCameraConfig.ExposureMode = MMAL_PARAM_EXPOSUREMODE_T.MMAL_PARAM_EXPOSUREMODE_BACKLIGHT;
            MMALCameraConfig.VideoResolution = new Resolution(capWidth, capHeight);
            MMALCameraConfig.VideoFramerate = new MMAL_RATIONAL_T(60, 1);

            using (var vidCaptureHandler = new EmguInMemoryCaptureHandler())
            using (var splitter = new MMALSplitterComponent())
            using (var renderer = new MMALNullSinkComponent()) {
                cam.ConfigureCameraSettings();
                LogUtil.Write("Cam mode is " + MMALCameraConfig.SensorMode);
                // Register to the event.
                vidCaptureHandler.MyEmguEvent += OnEmguEventCallback;

                // We are instructing the splitter to do a format conversion to BGR24.
                var splitterPortConfig = new MMALPortConfig(MMALEncoding.BGR24, MMALEncoding.BGR24, capWidth, capHeight, null);

                // By default in MMALSharp, the Video port outputs using proprietary communication (Opaque) with a YUV420 pixel format.
                // Changes to this are done via MMALCameraConfig.VideoEncoding and MMALCameraConfig.VideoSubformat.                
                splitter.ConfigureInputPort(new MMALPortConfig(MMALEncoding.OPAQUE, MMALEncoding.I420, capWidth, capHeight, null), cam.Camera.VideoPort, null);

                // We then use the splitter config object we constructed earlier. We then tell this output port to use our capture handler to record data.
                splitter.ConfigureOutputPort<SplitterVideoPort>(0, splitterPortConfig, vidCaptureHandler);

                cam.Camera.PreviewPort.ConnectTo(renderer);
                cam.Camera.VideoPort.ConnectTo(splitter);

                // Camera warm up time
                LogUtil.Write("Camera is warming up...");
                await Task.Delay(2000).ConfigureAwait(false);
                LogUtil.WriteInc("Camera initialized.");
                await cam.ProcessAsync(cam.Camera.VideoPort, ct);
                LogUtil.WriteDec("Camera closed.");
            }
        }

        protected virtual void OnEmguEventCallback(object sender, EmguEventArgs args) {
            var input = new Image<Bgr, byte>(capWidth, capHeight);
            input.Bytes = args.ImageData;
            Frame = input.Mat;
        }
        #region IDisposable Support
        private bool disposedValue;

        Mat IVideoStream.frame { get => Frame; set => Frame = value; }

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    cam.Cleanup();
                }
                disposedValue = true;
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }


    
}