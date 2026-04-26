#region

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Prismatik;

public class PrismatikDiscovery : ColorDiscovery, IColorDiscovery {
	private readonly List<string> _discoveredIds;

	public PrismatikDiscovery(ColorService cs) : base(cs) {
		_discoveredIds = new List<string>();
	}

	public async Task Discover(int timeout, CancellationToken ct) {
		_discoveredIds.Clear();
		Log.Debug("Prismatik: Discovery started...");

		var targets = DataUtil.GetItem<List<string>>("PrismatikTargets");
		if (targets == null || targets.Count == 0) {
			targets = new List<string> { "127.0.0.1" };
		}

		foreach (var entry in targets) {
			if (ct.IsCancellationRequested) break;

			var parts = entry.Split(':');
			var ip = parts[0];
			var port = 3636;
			if (parts.Length > 1) {
				int parsed;
				if (int.TryParse(parts[1], out parsed)) port = parsed;
			}

			try {
				using var tcp = new TcpClient();
				var connectTask = tcp.ConnectAsync(ip, port);
				var completed = await Task.WhenAny(connectTask, Task.Delay(TimeSpan.FromSeconds(3), ct));
				if (completed != connectTask || !tcp.Connected) continue;

				var stream = tcp.GetStream();
				stream.ReadTimeout = 3000;
				stream.WriteTimeout = 3000;

				var cmd = Encoding.ASCII.GetBytes("getstatusapi\n");
				await stream.WriteAsync(cmd, 0, cmd.Length, ct);

				var buffer = new byte[256];
				var read = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
				var response = Encoding.ASCII.GetString(buffer, 0, read).Trim();

				if (!response.Contains("statusapi:idle") && !response.Contains("statusapi:busy")) continue;

				var id = "prismatik_" + ip.Replace(".", "_") + "_" + port;
				if (_discoveredIds.Contains(id)) continue;

				Log.Debug("Prismatik: Device found at {Ip}:{Port} ({Status})", ip, port, response);

				var data = new PrismatikData(id, ip, port, "Prismatik " + ip, 0);
				var existing = DataUtil.GetDevice<PrismatikData>(id);
				if (existing != null) {
					data.Enable = existing.Enable;
					data.LedCount = existing.LedCount;
					data.ApiKey = existing.ApiKey;
				}

				await ControlService.AddDevice(data);
				_discoveredIds.Add(id);
			} catch (OperationCanceledException) {
				break;
			} catch (Exception ex) {
				Log.Debug("Prismatik: Error probing {Ip}:{Port} - {Message}", ip, port, ex.Message);
			}
		}

		Log.Debug("Prismatik: Discovery complete.");
	}
}
