using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Q42.HueApi;
using Q42.HueApi.Interfaces;
using Q42.HueApi.Models.Bridge;
using Serilog;

namespace Glimmr.Models.ColorTarget.Hue {
	public class HueDiscovery : ColorDiscovery, IColorDiscovery, IColorTargetAuth {
		public override string DeviceTag { get; set; } = "Hue";
		private readonly BridgeLocator _bridgeLocatorHttp;
		private readonly BridgeLocator _bridgeLocatorMdns;
		private readonly BridgeLocator _bridgeLocatorSsdp;
		private readonly ControlService _controlService;

		public HueDiscovery(ColorService colorService) : base(colorService) {
			_bridgeLocatorHttp = new HttpBridgeLocator();
			_bridgeLocatorMdns = new MdnsBridgeLocator();
			_bridgeLocatorSsdp = new SsdpBridgeLocator();
			_bridgeLocatorHttp.BridgeFound += DeviceFound;
			_bridgeLocatorMdns.BridgeFound += DeviceFound;
			_bridgeLocatorSsdp.BridgeFound += DeviceFound;
			_controlService = colorService.ControlService;
		}

		public async Task Discover(CancellationToken ct, int timeout) {
			Log.Debug("Hue: Discovery started...");
			try {
				await Task.WhenAll(_bridgeLocatorHttp.LocateBridgesAsync(ct), _bridgeLocatorMdns.LocateBridgesAsync(ct),
					_bridgeLocatorSsdp.LocateBridgesAsync(ct));
			} catch (Exception e) {
				Log.Debug("Hue discovery exception: " + e.Message);
			}

			Log.Debug("Hue: Discovery complete.");
		}

		public async Task<dynamic> CheckAuthAsync(dynamic dev) {
			var devData = (HueData) dev;
			try {
				ILocalHueClient client = new LocalHueClient(devData.IpAddress);
				//Make sure the user has pressed the button on the bridge before calling RegisterAsync
				//It will throw an LinkButtonNotPressedException if the user did not press the button
				var devName = Environment.MachineName;
				if (devName.Length > 19) devName = devName.Substring(0, 18);
				Log.Debug("Using device name for registration: " + devName);
				var result = await client.RegisterAsync("Glimmr", devName, true);
				if (result == null) {
					return devData;
				}

				if (string.IsNullOrEmpty(result.Username) || string.IsNullOrEmpty(result.StreamingClientKey)) {
					return devData;
				}

				devData.Token = result.StreamingClientKey;
				devData.User = result.Username;
				devData = UpdateDeviceData(devData);
				devData.Token = result.StreamingClientKey;
				devData.User = result.Username;
				return devData;
			} catch (HueException) {
				Log.Debug($@"Hue: The link button is not pressed at {devData.IpAddress}.");
			}

			return devData;
		}

		private void DeviceFound(IBridgeLocator sender, LocatedBridge locatedbridge) {
			var data = new HueData(locatedbridge);
			data = UpdateDeviceData(data);
			_controlService.AddDevice(data).ConfigureAwait(false);
		}

		private static HueData UpdateDeviceData(HueData data) {
			// Check for existing device
			HueData dev = DataUtil.GetDevice<HueData>(data.Id);
			
			if (dev != null && !string.IsNullOrEmpty(dev.Token)) {
				var client = new LocalHueClient(data.IpAddress, dev.User, dev.Token);
				try {
					var groups = client.GetGroupsAsync().Result;
					var lights = client.GetLightsAsync().Result;
					data.AddGroups(groups);
					data.AddLights(lights);
					dev.UpdateFromDiscovered(data);
					return dev;
				} catch (Exception e) {
					Log.Warning("Exception: " + e.Message);
				}
			}

			return data;
		}
	}
}