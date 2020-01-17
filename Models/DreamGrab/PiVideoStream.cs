using System;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.Structure;
using MMALSharp;
using MMALSharp.Common.Utility;
using MMALSharp.Components;
using MMALSharp.Handlers;
using MMALSharp.Native;
using MMALSharp.Ports;
using MMALSharp.Ports.Outputs;

namespace HueDream.Models.DreamGrab {
    public class PiVideoStream : IVideoStream, System.IDisposable {
        private MMALCamera cam;
        public Mat frame;
        private int capWidth;
        private int capHeight;
        public PiVideoStream(int width = 800, int height = 600) {
            cam = MMALCamera.Instance;
            capWidth = width;
            capHeight = height;
        }
       

        public class EmguEventArgs : EventArgs {
            public byte[] ImageData { get; set; }
        }

        public class EmguInMemoryCaptureHandler : InMemoryCaptureHandler, IVideoCaptureHandler {
            public event EventHandler<EmguEventArgs> MyEmguEvent;

            public override void Process(byte[] data, bool eos) {
                // The InMemoryCaptureHandler parent class has a property called "WorkingData". 
                // It is your responsibility to look after the clearing of this property.

                // The "eos" parameter indicates whether the MMAL buffer has an EOS parameter, if so, the data that's currently
                // stored in the "WorkingData" property plus the data found in the "data" parameter indicates you have a full image frame.

                // I suspect in here, you will want to have a separate thread which is responsible for sending data to EmguCV for processing?
                Console.WriteLine("I'm in here");

                base.Process(data, eos);

                if (eos) {
                    this.MyEmguEvent(this, new EmguEventArgs { ImageData = this.WorkingData.ToArray() });

                    this.WorkingData.Clear();
                    Console.WriteLine("I have a full frame. Clearing working data.");
                }
            }

            public void Split() {
                throw new NotImplementedException();
            }
        }

        public async Task Start(CancellationToken ct) {
            MMALCameraConfig.VideoResolution = new Resolution(capWidth, capHeight);

            // By default, video resolution is set to 1920x1080 which will probably be too large for your project. Set as appropriate using MMALCameraConfig.VideoResolution
            // The default framerate is set to 30fps. You can see what "modes" the different cameras support by looking:
            // https://github.com/techyian/MMALSharp/wiki/OmniVision-OV5647-Camera-Module
            // https://github.com/techyian/MMALSharp/wiki/Sony-IMX219-Camera-Module            
            using (var vidCaptureHandler = new EmguInMemoryCaptureHandler())
            using (var splitter = new MMALSplitterComponent())
            using (var renderer = new MMALNullSinkComponent()) {
                cam.ConfigureCameraSettings();

                // Register to the event.
                vidCaptureHandler.MyEmguEvent += OnEmguEventCallback;

                // We are instructing the splitter to do a format conversion to BGR24.
                var splitterPortConfig = new MMALPortConfig(MMALEncoding.BGR24, MMALEncoding.BGR24, 0, 0, null);

                // By default in MMALSharp, the Video port outputs using proprietary communication (Opaque) with a YUV420 pixel format.
                // Changes to this are done via MMALCameraConfig.VideoEncoding and MMALCameraConfig.VideoSubformat.                
                splitter.ConfigureInputPort(new MMALPortConfig(MMALEncoding.OPAQUE, MMALEncoding.I420), cam.Camera.VideoPort, null);

                // We then use the splitter config object we constructed earlier. We then tell this output port to use our capture handler to record data.
                splitter.ConfigureOutputPort<SplitterVideoPort>(0, splitterPortConfig, vidCaptureHandler);

                cam.Camera.PreviewPort.ConnectTo(renderer);
                cam.Camera.VideoPort.ConnectTo(splitter);

                // Camera warm up time
                await Task.Delay(2000).ConfigureAwait(false);

                await cam.ProcessAsync(cam.Camera.VideoPort, ct);
            }
        }

        protected virtual void OnEmguEventCallback(object sender, EmguEventArgs args) {
            Console.WriteLine("I'm in OnEmguEventCallback.");

            var input = new Image<Bgr, byte>(capWidth, capHeight);
            input.Bytes = args.ImageData;
            frame = input.Mat;
        }
        #region IDisposable Support
        private bool disposedValue = false;

        Mat IVideoStream.frame { get => frame; set => frame = value; }

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