using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.Structure;

namespace HueDream.Models.DreamGrab {
    public class WebCamVideoStream : IVideoStream {

        private VideoCapture video;
        private Image<Bgr, byte> frame;

        public WebCamVideoStream(int inputStream) {
            video = new VideoCapture(inputStream);
            frame = video.QueryFrame().ToImage<Bgr, byte>();
        }

        public Image<Bgr, byte> GetFrame() {
            return frame;
        }

        public async Task Start(CancellationToken ct) {
            while (!ct.IsCancellationRequested) {
                frame = video.QueryFrame().ToImage<Bgr, byte>();
            }
        }       
    }
}