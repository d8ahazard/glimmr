using System;
using System.Threading;
using System.Threading.Tasks;
using Corsair.CUE.SDK;
using Glimmr.Services;
using Newtonsoft.Json;
using Serilog;

namespace Glimmr.Models.ColorTarget.Corsair {
	public class CorsairDiscovery : ColorDiscovery, IColorDiscovery {
		private ControlService _controlService;
		public async Task Discover(CancellationToken ct) {
			Log.Debug("Corsair: Discovery started...");
			await FindDevices();
			Log.Debug("Corsair: Discovery complete.");
		}

		public CorsairDiscovery(ColorService colorService) : base(colorService) {
			DeviceTag = "Corsair";
			_controlService = colorService.ControlService;
		}

		public override string DeviceTag { get; set; }

		private async Task FindDevices() {
			var devs = CUESDK.CorsairGetDeviceCount();
			Log.Debug("Device count: " + devs);
			if (devs > 0) {
				for (var i = 0; i < devs; i++) {
					var info = CUESDK.CorsairGetDeviceInfo(i);
					//var layout = CUESDK.CorsairGetLedPositionsByDeviceIndex(i);
					//_devices[info.type] = layout;
					Log.Debug("Adding corsair device...");
					var dev = new CorsairData(i,info);
					Log.Debug("Device found: " + JsonConvert.SerializeObject(dev));
					await _controlService.AddDevice(dev);
				}
			}
		}
	}
}