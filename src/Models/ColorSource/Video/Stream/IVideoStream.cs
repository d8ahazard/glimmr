using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;

namespace Glimmr.Models.ColorSource.Video.Stream {
    public interface IVideoStream {
        public Task Start(CancellationToken ct);
        public Task Stop();
        public Mat Frame { get; set; }
    }
}