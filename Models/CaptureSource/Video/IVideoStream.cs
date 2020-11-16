using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;

namespace Glimmr.Models.CaptureSource.Video {
    public interface IVideoStream {
        public Task Start(CancellationToken ct);        
        public Mat Frame { get; set; }
    }
}