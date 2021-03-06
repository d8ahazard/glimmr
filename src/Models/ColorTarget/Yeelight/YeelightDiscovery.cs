﻿#region

using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Serilog;
using YeelightAPI;

#endregion

namespace Glimmr.Models.ColorTarget.Yeelight {
	public class YeelightDiscovery : ColorDiscovery, IColorDiscovery {
		public override string DeviceTag { get; set; } = "Yeelight";

		private readonly ControlService _controlService;

		public YeelightDiscovery(ColorService colorService) : base(colorService) {
			_controlService = colorService.ControlService;
		}

		public async Task Discover(CancellationToken ct, int timeout) {
			Log.Debug("Yeelight: Discovery started...");
			// Await the asynchronous call to the static API
			var discoveredDevices = await DeviceLocator.DiscoverAsync(ct);
			Log.Debug("Found yeelight devices: " + JsonConvert.SerializeObject(discoveredDevices));
			foreach (var dev in discoveredDevices) {
				Log.Debug("YEE YEE: " + JsonConvert.SerializeObject(dev));
				var ip = IpUtil.GetIpFromHost(dev.Hostname);
				var ipString = ip == null ? "" : ip.ToString();
				var yd = new YeelightData {
					Id = dev.Id, IpAddress = ipString, Name = dev.Name
				};
				await _controlService.AddDevice(yd);
			}

			Log.Debug("Yeelight: Discovery complete.");
		}
	}
}