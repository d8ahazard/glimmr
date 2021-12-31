#region

using System.Threading;
using System.Threading.Tasks;
using Glimmr.Services;

#endregion

namespace Glimmr.Models.ColorTarget;

public interface IColorDiscovery {
	public Task Discover(CancellationToken ct, int timeout);
}

public abstract class ColorDiscovery {
	public ControlService ControlService { get; }

	protected ColorDiscovery(ColorService colorService) {
		ControlService = colorService.ControlService;
	}
}