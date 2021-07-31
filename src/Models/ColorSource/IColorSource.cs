using System.Threading;
using System.Threading.Tasks;

namespace Glimmr.Models.ColorSource {
	public interface IColorSource {
		bool SourceActive { get; set; }

		public Task ToggleStream(CancellationToken ct);
		public void Refresh(SystemData systemData);
	}
}