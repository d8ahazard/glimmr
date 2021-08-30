#region

using System.Threading;
using System.Threading.Tasks;

#endregion

namespace Glimmr.Models.ColorSource {
	public interface IColorSource {
		bool SourceActive { get; }
		public Task ToggleStream(CancellationToken ct);
	}
}