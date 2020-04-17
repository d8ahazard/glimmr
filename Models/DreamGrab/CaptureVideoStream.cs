using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.Structure;

namespace HueDream.Models.DreamGrab {
    public class CaptureVideoStream : IVideoStream {
        Mat IVideoStream.Frame { get; }

        public Task Start(CancellationToken ct) {
            throw new System.NotImplementedException();
        }
    }
}