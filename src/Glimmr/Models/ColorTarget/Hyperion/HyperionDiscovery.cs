#region

using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json.Linq;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Hyperion;

public class HyperionDiscovery : ColorDiscovery, IColorDiscovery {
	public HyperionDiscovery(ColorService cs) : base(cs) {
	}

	public async Task Discover(int timeout, CancellationToken ct) {
		Log.Debug("Hyperion: Discovery started...");

		var existingDevices = DataUtil.GetDevices<HyperionData>("Hyperion");
		foreach (var device in existingDevices) {
			if (string.IsNullOrEmpty(device.IpAddress)) continue;

			try {
				await ProbeHost(device.IpAddress, device.Port, ct);
			} catch (Exception e) {
				Log.Debug("Hyperion: Discovery error for " + device.IpAddress + ": " + e.Message);
			}
		}

		Log.Debug("Hyperion: Discovery complete.");
	}

	private static async Task ProbeHost(string host, int port, CancellationToken ct) {
		using var tcp = new TcpClient();
		var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
		cts.CancelAfter(TimeSpan.FromSeconds(5));

		await tcp.ConnectAsync(host, port, cts.Token);
		var stream = tcp.GetStream();

		var cmd = "{\"command\":\"sysinfo\"}\n";
		var cmdBytes = Encoding.UTF8.GetBytes(cmd);
		await stream.WriteAsync(cmdBytes, cts.Token);
		await stream.FlushAsync(cts.Token);

		var buffer = new byte[4096];
		var sb = new StringBuilder();
		stream.ReadTimeout = 5000;

		var bytesRead = await stream.ReadAsync(buffer, cts.Token);
		if (bytesRead > 0) {
			sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
		}

		var response = sb.ToString().Trim();
		if (string.IsNullOrEmpty(response)) return;

		var json = JObject.Parse(response);
		var info = json["info"];
		var hyperionBuild = info?["hyperion_build"];
		var hostname = info?["hostname"]?.ToString() ?? host;

		if (hyperionBuild == null) return;

		var ledCount = 1;
		var leds = info?["leds"] as JArray;
		if (leds != null) {
			ledCount = leds.Count;
		}

		var data = new HyperionData {
			Id = $"hyperion_{host}_{port}",
			Name = $"Hyperion ({hostname})",
			IpAddress = host,
			Port = port,
			LedCount = ledCount > 0 ? ledCount : 1
		};

		await ControlService.AddDevice(data);
		Log.Debug("Hyperion: Found instance at " + host + ":" + port);
	}
}
