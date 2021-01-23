using System.Threading.Tasks;
using Glimmr.Models.Util;
using Newtonsoft.Json;
using Serilog;
using YeelightAPI;

namespace Glimmr.Models.ColorTarget.Yeelight {
	public static class YeelightDiscovery {
		
		public	static async Task Discover() {
			Log.Debug("Yeelight: Discovery started...");
			// Await the asynchronous call to the static API
			var discoveredDevices = await DeviceLocator.DiscoverAsync();
			foreach (var dev in discoveredDevices) {
				Log.Debug("YEE YEE: " + JsonConvert.SerializeObject(dev));
				var yd = new YeelightData {
					Id = dev.Id, IpAddress = IpUtil.GetIpFromHost(dev.Hostname).ToString(), Name = dev.Name
				};
				var existing = DataUtil.GetCollectionItem<YeelightData>("Dev_Yeelight", dev.Id);
				if (existing != null) {
					yd.CopyExisting(existing);
				}

				DataUtil.InsertCollection<YeelightData>("Dev_Yeelight", dev);
			}

			Log.Debug("Yeelight: Discovery complete.");
		}
	}
}