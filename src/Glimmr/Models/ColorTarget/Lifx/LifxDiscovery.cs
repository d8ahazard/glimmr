#region

using System;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Data;
using Glimmr.Services;
using LifxNetPlus;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Lifx;

public class LifxDiscovery : ColorDiscovery, IColorDiscovery {
	protected virtual string DeviceTag => "Bulb";
	private readonly LifxClient? _client;

	public LifxDiscovery(ColorService cs) : base(cs) {
		var client = cs.ControlService.GetAgent("LifxAgent");
		if (client == null) {
			return;
		}

		_client = client;
		_client.DeviceDiscovered += Client_DeviceDiscovered;
	}

	public async Task Discover(int timeout, CancellationToken ct) {
		if (_client == null) {
			return;
		}

		Log.Debug("Lifx: Discovery started.");
		_client.StartDeviceDiscovery();
		await Task.Delay(TimeSpan.FromSeconds(timeout), CancellationToken.None);
		_client.StopDeviceDiscovery();
		Log.Debug("Lifx: Discovery complete.");
	}

	private async void Client_DeviceDiscovered(object? sender, LifxClient.DeviceDiscoveryEventArgs e) {
		var bulb = e.Device;

		var ld = await GetBulbInfo(bulb);
		if (ld == null) {
			return;
		}

		Log.Debug("Adding device: " + JsonConvert.SerializeObject(ld));
		await ControlService.AddDevice(ld);
	}

	private async Task<LifxData?> GetBulbInfo(Device b) {
		if (_client == null) {
			return null;
		}

		var ver = await _client.GetDeviceVersionAsync(b);
		var hasMulti = false;
		var extended = false;
		var zoneCount = 0;
		var tag = DeviceTag;
		// Set multi zone stuff
		if (ver is { Product: 31 or 32 or 38 }) {
			tag = ver.Product == 38 ? "Beam" : "Z";
			hasMulti = true;
			if (ver.Product != 31) {
				if (ver.Version >= 1532997580) {
					extended = true;
				}
			}

			if (extended) {
				var zones = await _client.GetExtendedColorZonesAsync(b);
				if (zones != null) {
					zoneCount = zones.ZonesCount;
				}
			} else {
				// Original device only supports eight zones?
				var zones = await _client.GetColorZonesAsync(b, 0, 8);
				if (zones != null) {
					zoneCount = zones.Count;
				}
			}
		}

		var state = await _client.GetLightStateAsync(b);
		var power = false;
		ushort hue = 0;
		ushort saturation = 0;
		ushort brightness = 100;
		ushort kelvin = 0;
		if (state != null) {
			power = state.IsOn;
			hue = state.Hue;
			saturation = state.Saturation;
			brightness = state.Brightness;
			kelvin = state.Kelvin;
		}

		var d = new LifxData(b) {
			Power = power,
			Hue = hue,
			Saturation = saturation,
			Brightness = brightness,
			Kelvin = kelvin,
			TargetSector = -1,
			HasMultiZone = hasMulti,
			MultiZoneV2 = extended,
			MultiZoneCount = zoneCount
		};
		if (hasMulti && zoneCount != 0) {
			d.GenerateBeamLayout();
		}

		if (ver is { Product: 55 or 101 }) {
			tag = "Tile";
			try {
				var tData = _client.GetDeviceChainAsync(b).Result;
				if (tData != null) {
					d.Layout = new TileLayout(tData);
				}
			} catch (Exception e) {
				Log.Debug("Chain exception: " + e.Message);
			}
		}

		d.DeviceTag = tag;
		return d;
	}
}