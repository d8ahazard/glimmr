﻿#region

using System;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Services;
using LifxNetPlus;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Lifx {
	public class LifxDiscovery : ColorDiscovery, IColorDiscovery {
		public override string DeviceTag { get; set; } = "Lifx Bulb";
		private readonly LifxClient? _client;
		private readonly ControlService _controlService;

		public LifxDiscovery(ColorService cs) : base(cs) {
			var client = cs.ControlService.GetAgent("LifxAgent");
			_controlService = cs.ControlService;
			if (client == null) return;
			_client = client;
			_client.DeviceDiscovered += Client_DeviceDiscovered;
			_controlService = cs.ControlService;
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
			var bulb = e.Device;
			if (bulb == null) return;
			//Log.Debug("Device found: " + JsonConvert.SerializeObject(bulb));
			var ld = await GetBulbInfo(bulb);
			if (ld != null) await _controlService.AddDevice(ld);
		}

		private async Task<LifxData?> GetBulbInfo(Device b) {
			if (_client == null) return null;
			var state = await _client.GetLightStateAsync(b);
			var ver = await _client.GetDeviceVersionAsync(b);
			var hasMulti = false;
			var extended = false;
			var zoneCount = 0;
			var tag = DeviceTag;
			// Set multi zone stuff
			if (ver.Product == 31 || ver.Product == 32 || ver.Product == 38) {
				tag = ver.Product == 38 ? "Lifx Beam" : "Lifx Z";
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
				Brightness = 100,
				Kelvin = state.Kelvin,
				TargetSector = -1,
				HasMultiZone = hasMulti,
				MultiZoneV2 = extended,
				MultiZoneCount = zoneCount
			};
			if (hasMulti && zoneCount != 0) {
				d.GenerateBeamLayout();
			}

			if (ver.Product == 55 || ver.Product == 101) {
				tag = "Lifx Tile";
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