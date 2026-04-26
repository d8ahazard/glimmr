#region

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json.Linq;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Dirigera;

public class DirigeraDiscovery : ColorDiscovery, IColorDiscovery {
	private readonly List<string> _discoveredIds;

	private static readonly HttpClient HttpClient = new(new HttpClientHandler {
		ServerCertificateCustomValidationCallback = (_, _, _, _) => true
	}) {
		Timeout = TimeSpan.FromSeconds(10)
	};

	public DirigeraDiscovery(ColorService cs) : base(cs) {
		_discoveredIds = new List<string>();
	}

	public async Task Discover(int timeout, CancellationToken ct) {
		_discoveredIds.Clear();
		Log.Debug("Dirigera: Discovery started...");

		var hubIp = DataUtil.GetItem<string>("DirigeraHubIp");
		var accessToken = DataUtil.GetItem<string>("DirigeraAccessToken");

		if (string.IsNullOrEmpty(hubIp) || string.IsNullOrEmpty(accessToken)) {
			Log.Debug("Dirigera: Hub IP or access token not configured, skipping discovery.");
			return;
		}

		try {
			using var request = new HttpRequestMessage(HttpMethod.Get,
				$"https://{hubIp}:8443/v1/devices");
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

			var response = await HttpClient.SendAsync(request, ct);
			if (!response.IsSuccessStatusCode) {
				Log.Debug("Dirigera: API returned {Status}", response.StatusCode);
				return;
			}

			var body = await response.Content.ReadAsStringAsync(ct);
			var devices = JArray.Parse(body);

			foreach (var device in devices) {
				var deviceType = device.SelectToken("type")?.ToString();
				if (deviceType != "light") continue;

				var deviceId = device.SelectToken("id")?.ToString();
				var name = device.SelectToken("attributes.customName")?.ToString()
				           ?? device.SelectToken("attributes.model")?.ToString()
				           ?? "Dirigera Light";

				if (string.IsNullOrEmpty(deviceId)) continue;
				if (_discoveredIds.Contains(deviceId)) continue;

				var id = "dirigera_" + deviceId;
				Log.Debug("Dirigera: Light found - {Name} ({DeviceId})", name, deviceId);

				var data = new DirigeraData(id, hubIp, accessToken, deviceId, name);
				var existing = DataUtil.GetDevice<DirigeraData>(id);
				if (existing != null) {
					data.Enable = existing.Enable;
					data.Brightness = existing.Brightness;
					data.TargetSector = existing.TargetSector;
				}

				await ControlService.AddDevice(data);
				_discoveredIds.Add(deviceId);
			}
		} catch (OperationCanceledException) {
			// Expected
		} catch (Exception ex) {
			Log.Debug("Dirigera: Discovery error: " + ex.Message);
		}

		Log.Debug("Dirigera: Discovery complete.");
	}
}
