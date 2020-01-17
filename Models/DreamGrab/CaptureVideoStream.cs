using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.Structure;

namespace HueDream.Models.DreamGrab {
    public class CaptureVideoStream : IVideoStream {
        private Mat frame;
        Mat IVideoStream.frame { get => frame; set => frame=value; }

        public Task Start(CancellationToken ct) {
            throw new System.NotImplementedException();
        }

        public Mat GetFrame() {
            throw new System.NotImplementedException();
        }
    }
}