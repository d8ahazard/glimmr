using System.Threading;
using System.Threading.Tasks;
using Glimmr.Services;

namespace Glimmr.Models.ColorTarget {
	public interface IColorDiscovery {
		
		public async Task Discover(CancellationToken ct) {
			await Task.FromResult(true);
		}
	}

	public abstract class ColorDiscovery {
		private ControlService _controlService;
		public string DeviceTag { get; set; }
		public string DeviceClass { get; }

		protected ColorDiscovery(ControlService controlService) {
			_controlService = controlService;
		}
		
	}
}