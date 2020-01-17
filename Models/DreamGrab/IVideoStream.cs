using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.Structure;

namespace HueDream.Models.DreamGrab {
    public interface IVideoStream {
        public Task Start(CancellationToken ct);
        public Mat GetFrame(); 
    }
}