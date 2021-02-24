using System.Threading;
using System.Threading.Tasks;
using Glimmr.Services;
using Serilog;

namespace Glimmr.Models.ColorTarget.Razer {
	public class RazerDiscovery : ColorDiscovery, IColorDiscovery {
		
		public RazerDiscovery(ControlService controlService) : base(controlService) {
			DeviceTag = "Razer";
		}
		

		public async Task Discover(CancellationToken ct) {
			Log.Debug("Razer: Discovery started...");
			while (!ct.IsCancellationRequested) {
				await Task.Delay(1, ct);
			}

			Log.Debug("Razer: Discovery complete.");
		}
	}
}