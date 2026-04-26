#region

using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.RazerChroma;

public class RazerChromaDiscovery : ColorDiscovery, IColorDiscovery {
	private readonly ControlService _cs;

	public RazerChromaDiscovery(ColorService colorService) : base(colorService) {
		_cs = colorService.ControlService;
	}

	public async Task Discover(int timeout, CancellationToken ct) {
		Log.Debug("RazerChroma: Discovery started...");
		try {
			using var client = new HttpClient();
			client.Timeout = TimeSpan.FromSeconds(5);
			var initBody = new {
				title = "Glimmr",
				description = "Ambient lighting",
				author = new { name = "Glimmr", contact = "glimmr.tv" },
				device_supported = new[] { "keyboard", "mouse", "mousepad", "headset", "chromalink" },
				category = "application"
			};

			var json = JsonConvert.SerializeObject(initBody);
			using var content = new StringContent(json, Encoding.UTF8, "application/json");
			var response = await client.PostAsync("http://localhost:54235/razer/chromasdk", content, ct);
			if (response.IsSuccessStatusCode) {
				var responseBody = await response.Content.ReadAsStringAsync(ct);
				var result = JObject.Parse(responseBody);
				var sessionUrl = result["uri"]?.ToString() ?? "";
				var sessionId = result["sessionid"]?.ToString() ?? "RazerChroma_Local";

				if (!string.IsNullOrEmpty(sessionUrl)) {
					var data = new RazerChromaData("RazerChroma_" + sessionId, "Razer Chroma", sessionUrl);
					await ControlService.AddDevice(data);
					Log.Debug("RazerChroma: Device found. Session URL: " + sessionUrl);
				}
			}
		} catch (Exception e) {
			Log.Debug("RazerChroma: SDK not reachable - " + e.Message);
		}

		Log.Debug("RazerChroma: Discovery complete.");
	}
}
