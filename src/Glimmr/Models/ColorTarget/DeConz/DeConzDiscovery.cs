#region

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json.Linq;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.DeConz;

public class DeConzDiscovery : ColorDiscovery, IColorDiscovery {
	private readonly ControlService _cs;

	public DeConzDiscovery(ColorService colorService) : base(colorService) {
		_cs = colorService.ControlService;
	}

	public async Task Discover(int timeout, CancellationToken ct) {
		Log.Debug("DeConz: Discovery started...");

		var existing = DataUtil.GetDevices<DeConzData>("DeConz");
		if (existing.Count == 0) {
			Log.Debug("DeConz: No configured devices found. Set IP and API key to discover lights.");
			return;
		}

		foreach (var device in existing) {
			if (string.IsNullOrEmpty(device.IpAddress) || string.IsNullOrEmpty(device.ApiKey)) {
				continue;
			}

			await DiscoverLights(device.IpAddress, device.Port, device.ApiKey, ct);
		}

		Log.Debug("DeConz: Discovery complete.");
	}

	private async Task DiscoverLights(string ip, int port, string apiKey, CancellationToken ct) {
		try {
			using var client = new HttpClient();
			client.Timeout = TimeSpan.FromSeconds(5);
			var url = $"http://{ip}:{port}/api/{apiKey}/lights";
			var response = await client.GetStringAsync(url, ct);
			var lights = JObject.Parse(response);

			foreach (var (lightId, lightToken) in lights) {
				if (lightToken is not JObject light) {
					continue;
				}

				var type = light["type"]?.ToString() ?? "";
				if (!type.Contains("Color", StringComparison.OrdinalIgnoreCase) &&
				    !type.Contains("Extended color", StringComparison.OrdinalIgnoreCase)) {
					continue;
				}

				var name = light["name"]?.ToString() ?? $"DeConz Light {lightId}";
				var id = $"DeConz_{ip}_{lightId}";
				var data = new DeConzData(id, ip, port, apiKey, lightId, name);
				await ControlService.AddDevice(data);
				Log.Debug($"DeConz: Added light {name} (ID: {lightId})");
			}
		} catch (Exception e) {
			Log.Debug($"DeConz: Error discovering lights at {ip}:{port} - " + e.Message);
		}
	}
}
