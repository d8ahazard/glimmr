#region

using System;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Q42.HueApi;
using Q42.HueApi.Interfaces;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Hue;

public class HueDiscovery : ColorDiscovery, IColorDiscovery, IColorTargetAuth {
	public HueDiscovery(ColorService colorService) : base(colorService) {
	}

	public async Task Discover(int timeout, CancellationToken ct) {
		Log.Debug("Hue: V1 Discovery disabled...");
		await Task.FromResult(true);
	}

	public async Task<dynamic> CheckAuthAsync(dynamic dev) {
		var devData = (HueData)dev;
		try {
			ILocalHueClient client = new LocalHueClient(devData.IpAddress);
			//Make sure the user has pressed the button on the bridge before calling RegisterAsync
			//It will throw an LinkButtonNotPressedException if the user did not press the button
			var devName = Environment.MachineName;
			if (devName.Length > 19) {
				devName = devName[..18];
			}

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

	private static HueData UpdateDeviceData(HueData data) {
		// Check for existing device
		var dd = DataUtil.GetDevice<HueData>(data.Id);

		if (dd == null) {
			return data;
		}

		var dev = (HueData)dd;
		if (string.IsNullOrEmpty(dev.Token)) {
			return data;
		}

		var client = new LocalHueClient(data.IpAddress, dev.User, dev.Token);
		try {
			var groups = client.GetGroupsAsync().Result;
			var lights = client.GetLightsAsync().Result;
			data.AddGroups(groups);
			data.AddLights(lights);
			
			dev.UpdateFromDiscovered(data);
			Log.Debug("Returning dev: " + JsonConvert.SerializeObject(dev));
			return dev;
		} catch (Exception e) {
			Log.Warning("Hue Discovery Exception: " + e.Message + " at " + e.StackTrace);
		}

		return data;
	}
}