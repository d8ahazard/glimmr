#region

using System.Threading;
using System.Threading.Tasks;
using Glimmr.Services;

#endregion

namespace Glimmr.Models.ColorTarget;

public interface IColorDiscovery {
	public Task Discover(int timeout, CancellationToken ct);
}

public abstract class ColorDiscovery {
	protected ControlService ControlService { get; }

	protected ColorDiscovery(ColorService colorService) {
		ControlService = colorService.ControlService;
	}
}