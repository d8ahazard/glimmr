#region

using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Frame;

#endregion

namespace Glimmr.Models.ColorSource.Video.Stream;

public interface IVideoStream {
	public Task Start(FrameSplitter splitter, CancellationToken ct);
	public Task Stop();
}