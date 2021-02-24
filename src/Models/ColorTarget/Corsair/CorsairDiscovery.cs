using System.Threading;
using System.Threading.Tasks;
using Glimmr.Services;
using Serilog;

namespace Glimmr.Models.ColorTarget.Corsair {
	public class CorsairDiscovery : ColorDiscovery, IColorDiscovery {
		public async Task Discover(CancellationToken ct) {
			Log.Debug("Corsair: Discovery started...");
			while (!ct.IsCancellationRequested) {
				await Task.Delay(1,ct);
			}
			Log.Debug("Corsair: Discovery complete.");
		}

		public CorsairDiscovery(ControlService controlService) : base(controlService) {
			DeviceTag = "Corsair";		}
	}
}