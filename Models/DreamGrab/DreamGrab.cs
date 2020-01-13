using System.Threading;
using System.Threading.Tasks;
using Accord;
using Accord.Math.Optimization;
using Emgu.CV;
using Emgu.CV.Structure;
using Microsoft.Extensions.Hosting;

namespace HueDream.Models.DreamGrab {
    public class DreamGrab : IHostedService {
        private Mat orig_frame;
        private Mat edged_frame;
        private Mat warped_frame;
        private Point[] curr_target;
        private Point[] prev_target;
        private Mat curr_edged;
        private Mat prev_edged;
        private static int scale_height = 300;
        private static int scale_width = 400;
        private int camType;
        private int lineSensitivity;
        private int avgThreshold;
        private int cannyMin;
        private int cannyMix;
        private LedData ledData;
        
        public DreamGrab() {
            var dd = DreamData.GetStore();
            ledData = dd.GetItem<LedData>("ledData") ?? new LedData(true);
        }

        public IVideoStream GetCamera() {
            if (ledData.CamType == 0) { // 0 = pi module, 1 = webcam, 3 = capture?
                return new PiVideoStream();
            } else if (ledData.CamType == 1) {
                return new WebCamVideoStream(ledData.StreamId);
            } else {
                return new CaptureVideoStream();
            }
        }

        public Task StartAsync(CancellationToken cancellationToken) {
            var cam = GetCamera();
            while (!cancellationToken.IsCancellationRequested) {
                var frame = cam.GetFrame();
                if (ledData.CamType != 3) {
                    frame = ProcessFrame();
                }
            }
        }

        private Image<Bgr, byte> ProcessFrame() {
            
        }

        public Task StopAsync(CancellationToken cancellationToken) {
            throw new System.NotImplementedException();
        }
    }
}