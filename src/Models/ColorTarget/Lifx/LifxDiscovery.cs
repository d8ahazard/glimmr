using System;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Services;
using LifxNetPlus;
using Serilog;

namespace Glimmr.Models.ColorTarget.Lifx {
	public class LifxDiscovery : ColorDiscovery, IColorDiscovery {
		public override string DeviceTag { get; set; }
		private readonly LifxClient _client;
		private readonly ControlService _controlService;

		public LifxDiscovery(ColorService cs) : base(cs) {
			_client = cs.ControlService.GetAgent("LifxAgent");
			_client.DeviceDiscovered += Client_DeviceDiscovered;
			_controlService = cs.ControlService;
			DeviceTag = "Lifx";
		}

		public async Task Discover(CancellationToken ct, int timeout) {
			if (_client == null) {
				return;
			}

			Log.Debug("Lifx: Discovery started.");
			_client.StartDeviceDiscovery();
			await Task.Delay(TimeSpan.FromSeconds(timeout), CancellationToken.None);
			_client.StopDeviceDiscovery();
			Log.Debug("Lifx: Discovery complete.");
		}

		private async void Client_DeviceDiscovered(object sender, LifxClient.DeviceDiscoveryEventArgs e) {
			var bulb = e.Device as LightBulb;
			//Log.Debug("Device found: " + JsonConvert.SerializeObject(bulb));
			var ld = await GetBulbInfo(bulb);
			await _controlService.AddDevice(ld);
		}

		private async Task<LifxData> GetBulbInfo(LightBulb b) {
			var state = await _client.GetLightStateAsync(b);
			var ver = await _client.GetDeviceVersionAsync(b);
			var hasMulti = false;
			var extended = false;
			var zoneCount = 0;
			var tag = DeviceTag;
			// Set multi zone stuff
			if (ver.Product == 31 || ver.Product == 32 || ver.Product == 38) {
				tag = ver.Product == 38 ? "LifxBeam" : "LifxZ";
				hasMulti = true;
				if (ver.Product != 31) {
					if (ver.Version >= 1532997580) {
						extended = true;
					}
				}

				if (extended) {
					var zones = await _client.GetExtendedColorZonesAsync(b);
					zoneCount = zones.ZonesCount;
				} else {
					// Original device only supports eight zones?
					var zones = await _client.GetColorZonesAsync(b, 0, 8);
					zoneCount = zones.Count;
				}
			}

			var d = new LifxData(b) {
				Power = state.IsOn,
				Hue = state.Hue,
				Saturation = state.Saturation,
				Brightness = state.Brightness,
				Kelvin = state.Kelvin,
				TargetSector = -1,
				HasMultiZone = hasMulti,
				MultiZoneV2 = extended,
				MultiZoneCount = zoneCount
			};

			if (ver.Product == 55 || ver.Product == 101) {
				tag = "LifxTile";
				try {
					var tData = _client.GetDeviceChainAsync(b).Result;
					d.Layout = new TileLayout(tData);
				} catch (Exception e) {
					Log.Debug("Chain exception: " + e.Message);
				}
			}

			d.DeviceTag = tag;
			//Log.Debug("Discovered lifx device: " + JsonConvert.SerializeObject(d));
			return d;
		}
	}
}