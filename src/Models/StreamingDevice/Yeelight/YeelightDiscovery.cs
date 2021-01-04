using System.Collections.Generic;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Newtonsoft.Json;
using Serilog;
using YeelightAPI;

namespace Glimmr.Models.StreamingDevice.Yeelight {
	public class YeelightDiscovery {
		
		private	async Task<List<YeelightData>> GetDevicesAsync() {
			var output = new List<YeelightData>();
			// Await the asynchronous call to the static API
			IEnumerable<Device> discoveredDevices = await DeviceLocator.DiscoverAsync();
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