using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;

namespace HueDream.Models.Capture {
    public interface IVideoStream {
        public Task Start(CancellationToken ct);        
        public Mat Frame { get; }
    }
}