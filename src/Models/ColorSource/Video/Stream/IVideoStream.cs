#region

using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;

#endregion

namespace Glimmr.Models.ColorSource.Video.Stream {
	public interface IVideoStream {
		public Mat Frame { get; }
		public Task Start(CancellationToken ct);
		public Task Stop();
		public Task Refresh();
	}
}