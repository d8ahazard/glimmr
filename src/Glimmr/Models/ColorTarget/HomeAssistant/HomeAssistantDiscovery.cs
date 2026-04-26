#region

using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.HomeAssistant;

public class HomeAssistantDiscovery : ColorDiscovery, IColorDiscovery {
	public HomeAssistantDiscovery(ColorService cs) : base(cs) {
	}

	public async Task Discover(int timeout, CancellationToken ct) {
		Log.Debug("HomeAssistant: Discovery started...");

		var existingDevices = DataUtil.GetDevices<HomeAssistantData>("HomeAssistant");
		foreach (var device in existingDevices) {
			if (string.IsNullOrEmpty(device.HaUrl) || string.IsNullOrEmpty(device.AccessToken)) {
				continue;
			}

			try {
				await DiscoverEntities(device.HaUrl, device.AccessToken, ct);
			} catch (Exception e) {
				Log.Debug("HomeAssistant: Discovery error: " + e.Message);
			}
		}

		Log.Debug("HomeAssistant: Discovery complete.");
	}

	private static async Task DiscoverEntities(string haUrl, string accessToken, CancellationToken ct) {
		using var ws = new ClientWebSocket();
		var uri = new Uri($"ws://{haUrl}/api/websocket");
		var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
		cts.CancelAfter(TimeSpan.FromSeconds(15));

		await ws.ConnectAsync(uri, cts.Token);

		var authRequired = await ReceiveMessage(ws, cts.Token);
		if (authRequired == null) return;

		var msgId = 1;
		var authMsg = JsonConvert.SerializeObject(new { type = "auth", access_token = accessToken });
		await SendMessage(ws, authMsg, cts.Token);

		var authResult = await ReceiveMessage(ws, cts.Token);
		if (authResult == null) return;

		var authObj = JObject.Parse(authResult);
		if (authObj["type"]?.ToString() != "auth_ok") {
			Log.Warning("HomeAssistant: Authentication failed.");
			return;
		}

		var getStates = JsonConvert.SerializeObject(new { id = msgId++, type = "get_states" });
		await SendMessage(ws, getStates, cts.Token);

		var statesResponse = await ReceiveMessage(ws, cts.Token);
		if (statesResponse == null) return;

		var statesObj = JObject.Parse(statesResponse);
		var results = statesObj["result"] as JArray;
		if (results == null) return;

		foreach (var entity in results) {
			var entityId = entity["entity_id"]?.ToString() ?? "";
			if (!entityId.StartsWith("light.")) continue;

			var attrs = entity["attributes"];
			if (attrs == null) continue;

			var hasRgb = attrs["rgb_color"] != null || attrs["hs_color"] != null;
			if (!hasRgb) continue;

			var friendlyName = attrs["friendly_name"]?.ToString() ?? entityId;
			var haData = new HomeAssistantData {
				Id = entityId,
				Name = friendlyName,
				EntityId = entityId,
				HaUrl = haUrl,
				AccessToken = accessToken,
				IpAddress = haUrl.Contains(':') ? haUrl.Split(':')[0] : haUrl
			};

			await ControlService.AddDevice(haData);
			Log.Debug("HomeAssistant: Found light entity: " + entityId);
		}

		await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
	}

	private static async Task SendMessage(ClientWebSocket ws, string message, CancellationToken ct) {
		var bytes = Encoding.UTF8.GetBytes(message);
		await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
	}

	private static async Task<string?> ReceiveMessage(ClientWebSocket ws, CancellationToken ct) {
		var buffer = new byte[8192];
		var sb = new StringBuilder();
		WebSocketReceiveResult result;
		do {
			result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
			sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
		} while (!result.EndOfMessage);

		return sb.Length > 0 ? sb.ToString() : null;
	}
}
