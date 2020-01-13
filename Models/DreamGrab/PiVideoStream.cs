using System;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.Structure;
using MMALSharp;
using MMALSharp.Common.Utility;
using MMALSharp.Handlers;

namespace HueDream.Models.DreamGrab {
    public class PiVideoStream : IVideoStream, System.IDisposable {
        private MMALCamera cam;
        private Image<Bgr, byte> frame;
        public PiVideoStream() {
            cam = MMALCamera.Instance;    
            MMALCameraConfig.VideoResolution = new Resolution(800, 600);
            cam.ConfigureCameraSettings();
        }

        public async Task Start(CancellationToken ct) {
            using (var vidCaptureHandler = new InMemoryCaptureHandler()) {
                frame = new Image<Bgr, byte>(800, 600);
                while (!ct.IsCancellationRequested) {
                    await cam.TakeVideo(vidCaptureHandler, CancellationToken.None);
                    var bytes = vidCaptureHandler.WorkingData;
                    frame.Bytes = bytes.ToArray();
                }
            }
            cam.Cleanup();
        }

      
        public Image<Bgr, byte> GetFrame() {
            return frame;
        }

        #region IDisposable Support
        private bool disposedValue = false;

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