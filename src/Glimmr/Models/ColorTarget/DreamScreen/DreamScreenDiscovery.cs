#region

using System;
using System.Threading;
using System.Threading.Tasks;
using DreamScreenNet;
using Glimmr.Services;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.DreamScreen;

public class DreamScreenDiscovery : ColorDiscovery, IColorDiscovery {
	private readonly DreamScreenClient? _client;

	public DreamScreenDiscovery(ColorService colorService) : base(colorService) {
		_client = colorService.ControlService.GetAgent("DreamAgent");
	}

	public async Task Discover(int timeout, CancellationToken ct) {
		if (_client == null) {
			Log.Warning("DS Client is null...this is bad.");
			return;
		}

		Log.Debug("DS: Starting discovery...");
		_client.DeviceDiscovered += DeviceFound;
		_client.StartDeviceDiscovery();
		await Task.Delay(TimeSpan.FromSeconds(timeout), CancellationToken.None);
		_client.StopDeviceDiscovery();
		_client.DeviceDiscovered -= DeviceFound;
		Log.Debug("DS: Discovery complete.");
	}

	private static void DeviceFound(object? sender, DreamScreenClient.DeviceDiscoveryEventArgs e) {
		Log.Debug("Dream Device found??");
		var dd = new DreamScreenData(e.Device);
		Log.Debug("Got one: " + JsonConvert.SerializeObject(dd));
		ControlService.AddDevice(dd).ConfigureAwait(false);
	}
}