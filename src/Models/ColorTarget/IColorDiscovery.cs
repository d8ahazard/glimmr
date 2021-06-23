using System.Threading;
using System.Threading.Tasks;
using Glimmr.Services;

namespace Glimmr.Models.ColorTarget {
	public interface IColorDiscovery {
		public Task Discover(CancellationToken ct, int timeout);
	}

	public abstract class ColorDiscovery {
		private ColorService ColorService { get; set; }
		public ControlService ControlService { get; set; }
		public abstract string DeviceTag { get; set; }

		protected ColorDiscovery(ColorService colorService) {
			ColorService = colorService;
			ControlService = colorService.ControlService;
		}
	}
}