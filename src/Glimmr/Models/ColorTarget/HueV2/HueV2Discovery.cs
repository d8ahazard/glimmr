#region

using System;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using HueApi;
using HueApi.BridgeLocator;
using Q42.HueApi;
using Serilog;
using HttpBridgeLocator = HueApi.BridgeLocator.HttpBridgeLocator;

#endregion

namespace Glimmr.Models.ColorTarget.HueV2;

public class HueV2Discovery : ColorDiscovery, IColorDiscovery, IColorTargetAuth {
	private readonly HttpBridgeLocator _bridgeLocatorHttp;

	public HueV2Discovery(ColorService colorService) : base(colorService) {
		_bridgeLocatorHttp = new HttpBridgeLocator();
		_bridgeLocatorHttp.BridgeFound += DeviceFound;
	}

	public async Task Discover(int timeout, CancellationToken ct) {
		try {
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


	private static void DeviceFound(IBridgeLocator bridgeLocator, LocatedBridge locatedBridge) {
		var data = new HueV2Data(locatedBridge);
		data = UpdateDeviceData(data);
		ControlService.AddDevice(data).ConfigureAwait(false);
	}

	private static HueV2Data UpdateDeviceData(HueV2Data data) {
		// Check for existing device
		var appKey = string.Empty;
		var token = string.Empty;
		HueV2Data? dev = null;

		var dd = DataUtil.GetDevice<HueV2Data>(data.Id);
		if (dd != null) {
			Log.Debug("Found V2 Data!");
			dev = (HueV2Data)dd;
			appKey = dev.AppKey;
			token = dev.Token;
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