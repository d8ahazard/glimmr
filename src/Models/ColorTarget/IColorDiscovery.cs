using System;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Services;

namespace Glimmr.Models.ColorTarget {
	public interface IColorDiscovery {

		public Task Discover(CancellationToken ct);
	}

	public abstract class ColorDiscovery {
		public abstract string DeviceTag { get; set; }
		public ControlService ControlService { get; set; }
		public ColorService ColorService { get; set; }

		protected ColorDiscovery(ColorService colorService) {
			ColorService = colorService;
			ControlService = colorService.ControlService;
		}
		
	}
}