#region

using System.Threading;
using System.Threading.Tasks;

#endregion

namespace Glimmr.Models.ColorSource.Video.Stream {
	public interface IVideoStream {
		public Task Start(CancellationToken ct, FrameSplitter splitter);
		public Task Stop();
		public Task Refresh();
	}
}