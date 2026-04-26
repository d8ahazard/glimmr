#region

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json.Linq;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.LedFx;

public class LedFxDiscovery : ColorDiscovery, IColorDiscovery {
	private readonly List<string> _discoveredIds;

	private static readonly HttpClient HttpClient = new() {
		Timeout = TimeSpan.FromSeconds(5)
	};

	public LedFxDiscovery(ColorService cs) : base(cs) {
		_discoveredIds = new List<string>();
	}

	public async Task Discover(int timeout, CancellationToken ct) {
		_discoveredIds.Clear();
		Log.Debug("LedFx: Discovery started...");

		var targets = DataUtil.GetItem<List<string>>("LedFxTargets");
		if (targets == null || targets.Count == 0) {
			Log.Debug("LedFx: No target IPs configured, skipping discovery.");
			return;
		}

		foreach (var entry in targets) {
			if (ct.IsCancellationRequested) break;

			var parts = entry.Split(':');
			var ip = parts[0];
			var port = 8888;
			if (parts.Length > 1) {
				int parsed;
				if (int.TryParse(parts[1], out parsed)) port = parsed;
			}

			try {
				var response = await HttpClient.GetAsync($"http://{ip}:{port}/api/info", ct);
				if (!response.IsSuccessStatusCode) continue;

				var body = await response.Content.ReadAsStringAsync(ct);
				var obj = JObject.Parse(body);
				var name = obj.SelectToken("info.name")?.ToString() ?? "LedFx";
				var version = obj.SelectToken("info.version")?.ToString() ?? "";

				var id = "ledfx_" + ip.Replace(".", "_") + "_" + port;
				if (_discoveredIds.Contains(id)) continue;

				Log.Debug("LedFx: Instance found at {Ip}:{Port} - {Name} v{Version}", ip, port, name, version);

				var data = new LedFxData(id, ip, port, name + (string.IsNullOrEmpty(version) ? "" : " v" + version));
				var existing = DataUtil.GetDevice<LedFxData>(id);
				if (existing != null) {
					data.Enable = existing.Enable;
				}

				await ControlService.AddDevice(data);
				_discoveredIds.Add(id);
			} catch (OperationCanceledException) {
				break;
			} catch (Exception ex) {
				Log.Debug("LedFx: Error probing {Ip}:{Port} - {Message}", ip, port, ex.Message);
			}
		}

		Log.Debug("LedFx: Discovery complete.");
	}
}
