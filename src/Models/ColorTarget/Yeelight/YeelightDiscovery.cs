using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Serilog;
using YeelightAPI;

namespace Glimmr.Models.ColorTarget.Yeelight {
	public class YeelightDiscovery : ColorDiscovery, IColorDiscovery {

		private readonly ControlService _controlService;
		public YeelightDiscovery(ColorService colorService) : base(colorService) {
			_controlService = colorService.ControlService;
			DeviceTag = "Yeelight";
		}
		
		public async Task Discover(CancellationToken ct, int timeout) {
			Log.Debug("Yeelight: Discovery started...");
			// Await the asynchronous call to the static API
			var discoveredDevices = await DeviceLocator.DiscoverAsync(ct);
			foreach (var dev in discoveredDevices) {
				Log.Debug("YEE YEE: " + JsonConvert.SerializeObject(dev));
				var yd = new YeelightData {
					Id = dev.Id, IpAddress = IpUtil.GetIpFromHost(dev.Hostname).ToString(), Name = dev.Name
				};
				await _controlService.AddDevice(yd);
			}

			Log.Debug("Yeelight: Discovery complete.");
		}

		public override string DeviceTag { get; set; }
	}
}