#region

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.ColorTarget.Hue;
using Glimmr.Models.Util;
using Glimmr.Services;
using HueApi;
using HueApi.BridgeLocator;
using HueApi.Models;
using Newtonsoft.Json;
using Q42.HueApi;
using Serilog;
using HttpBridgeLocator = HueApi.BridgeLocator.HttpBridgeLocator;
using Light = HueApi.Models.Light;

#endregion

namespace Glimmr.Models.ColorTarget.HueV2;

public class HueV2Discovery : ColorDiscovery, IColorDiscovery, IColorTargetAuth {
	private readonly HttpBridgeLocator _bridgeLocatorHttp;
	private readonly ControlService _controlService;

	public HueV2Discovery(ColorService colorService) : base(colorService) {
		_bridgeLocatorHttp = new HttpBridgeLocator();
		_bridgeLocatorHttp.BridgeFound += DeviceFound;
		_controlService = colorService.ControlService;
	}

	public async Task Discover(int timeout, CancellationToken ct) {
		try {
			LoadTestJson();
			await _bridgeLocatorHttp.LocateBridgesAsync(ct);
		} catch (Exception e) {
			if (!e.Message.Contains("canceled")) {
				Log.Debug("Hue discovery exception: " + e.Message);
			}
		}

		Log.Debug("Hue: Discovery complete.");
	}

	public async Task<dynamic> CheckAuthAsync(dynamic dev) {
		Log.Debug("Checking auth...");
		var devData = (HueV2Data)dev;
		try {
			var client = string.IsNullOrEmpty(devData.AppKey)
				? new LocalHueClient(devData.IpAddress)
				: new LocalHueClient(devData.IpAddress, devData.AppKey);
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
			devData.AppKey = result.Username;
			devData = UpdateDeviceData(devData);
			devData = UpdateDeviceData(devData);
			devData.Token = result.StreamingClientKey;
			devData.AppKey = result.Username;
			return devData;
		} catch (LinkButtonNotPressedException) {
			Log.Debug($@"Hue: The link button is not pressed at {devData.IpAddress}.");
		} catch (Exception e) {
			Log.Warning("Exception linking hue: " + e.Message + " at " + e.StackTrace);
		}

		Log.Debug("Returning...");
		return devData;
	}

	private void LoadTestJson() {
		var path = SystemUtil.GetUserDir();
		var jsonPath = Path.Join(path, "hue_lights.json");
		var jsonPath2 = Path.Join(path, "hue_entertainment_config.json");
		var jsonPath3 = Path.Join(path, "hue_entertainment.json");
		if (!File.Exists(jsonPath) || !File.Exists(jsonPath2) || !File.Exists(jsonPath3)) {
			return;
		}

		var json = File.ReadAllText(jsonPath);
		var json2 = File.ReadAllText(jsonPath2);
		var json3 = File.ReadAllText(jsonPath3);
		try {
			var groups = JsonConvert.DeserializeObject<HueResponse<Entertainment>>(json3);
			var entData = JsonConvert.DeserializeObject<HueResponse<EntertainmentConfiguration>>(json2);
			var lightData = JsonConvert.DeserializeObject<HueResponse<Light>>(json);
			var testData = new HueV2Data {
				Id = "GregBridge",
				Name = "Greg's Bridge",
				Token = "WOO",
				AppKey = "FOO"
			};
			if (entData != null && groups != null && lightData != null) {
				Log.Debug("Configuring test data ent...");
				testData.ConfigureEntertainment(entData.Data, groups.Data, lightData.Data, true);
				ControlService.AddDevice(testData).ConfigureAwait(true);
			} else {
				Log.Warning("Unable to load entertainment data.");
			}
			//testData.ConfigureEntertainment(bridgeData);
		} catch (Exception e) {
			Log.Debug("Exception parsing bridge data: " + e.Message);
		}
	}

	private void DeviceFound(IBridgeLocator bridgeLocator, LocatedBridge locatedBridge) {
		var data = new HueV2Data(locatedBridge);
		data = UpdateDeviceData(data);
		Log.Debug("Found device: " + JsonConvert.SerializeObject(data));
		_controlService.AddDevice(data).ConfigureAwait(false);
	}

	private static HueV2Data UpdateDeviceData(HueV2Data data) {
		// Check for existing device
		var appKey = string.Empty;
		var token = string.Empty;
		var deleteV1 = false;
		HueV2Data? dev = null;

		try {
			var d1 = DataUtil.GetDevice<HueData>(data.Id[..^2]);

			if (d1 != null) {
				deleteV1 = true;
				var oldDev = (HueData)d1;
				appKey = oldDev.User;
				token = oldDev.Token;
			}
		} catch (Exception e) {
			Log.Debug("Exception with old dd: " + e.Message);
		}

		var dd = DataUtil.GetDevice<HueV2Data>(data.Id);
		if (dd != null) {
			Log.Debug("Found V2 Data!");
			dev = (HueV2Data)dd;
			appKey = dev.AppKey;
			token = dev.Token;
			if (deleteV1) {
				Log.Debug("Can't delete V1 device, dupe IDs. it's still in use.");
			}

			deleteV1 = false;
		}

		if (deleteV1) {
			Log.Debug("Deleting v1 device.");
			DataUtil.DeleteDevice(data.Id[..^2]);
		}

		if (string.IsNullOrEmpty(appKey)) {
			Log.Debug("No app key, returning..");
			return data;
		}

		data.AppKey = appKey;
		data.Token = token;
		Log.Debug("Connecting to " + data.IpAddress + " with key: " + data.AppKey);
		var client = new LocalHueApi(data.IpAddress, appKey);
		try {
			var groups = client.GetEntertainmentConfigurations().Result.Data;
			var devs = client.GetEntertainmentServices().Result.Data;
			var lights = client.GetLights().Result.Data;
			data.ConfigureEntertainment(groups, devs, lights);
			if (dev != null) {
				dev.UpdateFromDiscovered(data);
			} else {
				dev = data;
			}

			return dev;
		} catch (Exception e) {
			Log.Warning("Hue Discovery Exception: " + e.Message + " at " + e.StackTrace);
		}

		return data;
	}
}