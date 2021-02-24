using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Serilog;
using YeelightAPI;

namespace Glimmr.Models.ColorTarget.Yeelight {
	public class YeelightDiscovery : ColorDiscovery, IColorDiscovery {

		private ControlService _controlService;
		public YeelightDiscovery(ControlService controlService) : base(controlService) {
			_controlService = controlService;
			DeviceTag = "Yeelight";
		}
		
		public async Task Discover(CancellationToken ct) {
			Log.Debug("Yeelight: Discovery started...");
			// Await the asynchronous call to the static API
			var discoveredDevices = await DeviceLocator.DiscoverAsync(ct);
			foreach (var dev in discoveredDevices) {
				Log.Debug("YEE YEE: " + JsonConvert.SerializeObject(dev));
				var yd = new YeelightData {
					Id = dev.Id, IpAddress = IpUtil.GetIpFromHost(dev.Hostname).ToString(), Name = dev.Name
				};
				var existing = DataUtil.GetCollectionItem<YeelightData>("Dev_Yeelight", dev.Id);
				if (existing != null) {
					yd.CopyExisting(existing);
				}

				await DataUtil.InsertCollection<YeelightData>("Dev_Yeelight", dev);
			}

			Log.Debug("Yeelight: Discovery complete.");
		}
	}
}