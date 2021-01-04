using System.Collections.Generic;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Newtonsoft.Json;
using Serilog;
using YeelightAPI;

namespace Glimmr.Models.StreamingDevice.Yeelight {
	public static class YeelightDiscovery {
		
		public	static async Task<List<YeelightData>> Discover() {
			var output = new List<YeelightData>();
			// Await the asynchronous call to the static API
			var discoveredDevices = await DeviceLocator.DiscoverAsync();
			foreach (var dev in discoveredDevices) {
				Log.Debug("YEE YEE: " + JsonConvert.SerializeObject(dev));
				var yd = new YeelightData {
					Id = dev.Id, IpAddress = IpUtil.GetIpFromHost(dev.Hostname).ToString(), Name = dev.Name
				};
				output.Add(yd);
			}
			return output;
		}
	}
}