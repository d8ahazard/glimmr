#region

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Govee;

public class GoveeDiscovery : ColorDiscovery, IColorDiscovery {
	private readonly ControlService _controlService;
	private readonly List<string> _discoveredIds;
	private static readonly HttpClient HttpClient = new();

	private const string MulticastAddress = "239.255.255.250";
	private const int ScanPort = 4001;
	private const int ListenPort = 4002;

	private static readonly string ScanMessage =
		JsonConvert.SerializeObject(new {
			msg = new {
				cmd = "scan",
				data = new { account_topic = "reserve" }
			}
		});

	public GoveeDiscovery(ColorService cs) : base(cs) {
		_controlService = cs.ControlService;
		_discoveredIds = new List<string>();
	}

	public async Task Discover(int timeout, CancellationToken ct) {
		_discoveredIds.Clear();
		Log.Debug("Govee: Discovery started...");

		await DiscoverLan(timeout, ct);
		await DiscoverCloud(ct);

		Log.Debug("Govee: Discovery complete.");
	}

	private async Task DiscoverLan(int timeout, CancellationToken ct) {
		UdpClient? listener = null;
		try {
			listener = new UdpClient();
			listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
			listener.Client.Bind(new IPEndPoint(IPAddress.Any, ListenPort));
			listener.JoinMulticastGroup(IPAddress.Parse(MulticastAddress));

			var scanBytes = Encoding.UTF8.GetBytes(ScanMessage);
			var scanEndpoint = new IPEndPoint(IPAddress.Parse(MulticastAddress), ScanPort);
			await listener.SendAsync(scanBytes, scanBytes.Length, scanEndpoint);

			var deadline = DateTime.UtcNow.AddSeconds(timeout);
			while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested) {
				var receiveTask = listener.ReceiveAsync();
				var remaining = deadline - DateTime.UtcNow;
				if (remaining <= TimeSpan.Zero) break;

				var completed = await Task.WhenAny(receiveTask, Task.Delay(remaining, ct));
				if (completed != receiveTask) break;

				try {
					var result = await receiveTask;
					var json = Encoding.UTF8.GetString(result.Buffer);
					ProcessLanResponse(json, result.RemoteEndPoint.Address.ToString());
				} catch (Exception ex) {
					Log.Debug("Govee: Error processing LAN response: " + ex.Message);
				}
			}
		} catch (OperationCanceledException) {
			// Expected on cancellation
		} catch (Exception ex) {
			Log.Debug("Govee: LAN discovery error: " + ex.Message);
		} finally {
			if (listener != null) {
				try {
					listener.DropMulticastGroup(IPAddress.Parse(MulticastAddress));
				} catch {
					// ignored
				}

				listener.Dispose();
			}
		}
	}

	private void ProcessLanResponse(string json, string senderIp) {
		try {
			var obj = JObject.Parse(json);
			var cmd = obj.SelectToken("msg.cmd")?.ToString();
			if (cmd != "scan") return;

			var ip = obj.SelectToken("msg.data.ip")?.ToString() ?? senderIp;
			var device = obj.SelectToken("msg.data.device")?.ToString();
			var sku = obj.SelectToken("msg.data.sku")?.ToString() ?? "";

			if (string.IsNullOrEmpty(device)) return;
			if (_discoveredIds.Contains(device)) return;

			Log.Debug("Govee: LAN device found - {Device} at {Ip}, SKU: {Sku}", device, ip, sku);

			var data = new GoveeData(device, ip, sku, "Govee " + sku) { LanEnabled = true };
			var existing = DataUtil.GetDevice<GoveeData>(device);
			if (existing != null) {
				data.GoveeApiKey = existing.GoveeApiKey;
				data.LedCount = existing.LedCount > 0 ? existing.LedCount : data.LedCount;
				data.Enable = existing.Enable;
			}

			ControlService.AddDevice(data).ConfigureAwait(false);
			_discoveredIds.Add(device);
		} catch (Exception ex) {
			Log.Debug("Govee: Error parsing LAN response: " + ex.Message);
		}
	}

	private async Task DiscoverCloud(CancellationToken ct) {
		var apiKey = DataUtil.GetItem<string>("GoveeApiKey");
		if (string.IsNullOrEmpty(apiKey)) {
			Log.Debug("Govee: No API key configured, skipping cloud discovery.");
			return;
		}

		try {
			using var request = new HttpRequestMessage(HttpMethod.Get,
				"https://openapi.api.govee.com/router/api/v1/user/devices");
			request.Headers.Add("Govee-API-Key", apiKey);
			request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

			var response = await HttpClient.SendAsync(request, ct);
			if (!response.IsSuccessStatusCode) {
				Log.Debug("Govee: Cloud API returned {Status}", response.StatusCode);
				return;
			}

			var body = await response.Content.ReadAsStringAsync(ct);
			var obj = JObject.Parse(body);
			var devices = obj.SelectToken("data") as JArray;
			if (devices == null) return;

			foreach (var dev in devices) {
				var deviceId = dev.SelectToken("device")?.ToString();
				var sku = dev.SelectToken("sku")?.ToString() ?? "";
				var name = dev.SelectToken("deviceName")?.ToString() ?? "Govee " + sku;

				if (string.IsNullOrEmpty(deviceId)) continue;
				if (_discoveredIds.Contains(deviceId)) continue;

				var caps = dev.SelectToken("capabilities") as JArray;
				var supportsSegments = false;
				if (caps != null) {
					foreach (var cap in caps) {
						if (cap.SelectToken("instance")?.ToString() == "segmentedColorRgb") {
							supportsSegments = true;
							break;
						}
					}
				}

				var data = new GoveeData(deviceId, "", sku, name) {
					LanEnabled = false,
					GoveeApiKey = apiKey ?? "",
					SupportsSegments = supportsSegments
				};

				var existing = DataUtil.GetDevice<GoveeData>(deviceId);
				if (existing != null) {
					if (!string.IsNullOrEmpty(existing.IpAddress)) {
						data.IpAddress = existing.IpAddress;
						data.LanEnabled = true;
					}

					data.LedCount = existing.LedCount > 0 ? existing.LedCount : data.LedCount;
					data.Enable = existing.Enable;
				}

				Log.Debug("Govee: Cloud device found - {Device}, SKU: {Sku}", deviceId, sku);
				await ControlService.AddDevice(data);
				_discoveredIds.Add(deviceId);
			}
		} catch (OperationCanceledException) {
			// Expected
		} catch (Exception ex) {
			Log.Debug("Govee: Cloud discovery error: " + ex.Message);
		}
	}
}
