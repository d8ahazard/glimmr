#region

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Wiz;

public class WizDiscovery : ColorDiscovery, IColorDiscovery {
	private const int WizPort = 38899;

	public WizDiscovery(ColorService cs) : base(cs) { }

	public async Task Discover(int timeout, CancellationToken ct) {
		Log.Debug("WiZ: Discovery started...");
		var discovered = new HashSet<string>();

		try {
			using var udp = new UdpClient();
			udp.EnableBroadcast = true;
			udp.Client.ReceiveTimeout = timeout * 1000;
			udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

			var payload = Encoding.UTF8.GetBytes("{\"method\":\"getDevInfo\",\"params\":{}}");
			await udp.SendAsync(payload, payload.Length, new IPEndPoint(IPAddress.Broadcast, WizPort));

			var deadline = DateTime.UtcNow.AddSeconds(timeout);
			while (DateTime.UtcNow < deadline) {
				if (ct.IsCancellationRequested) break;

				try {
					var asyncResult = udp.BeginReceive(null, null);
					var completed = asyncResult.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));
					if (!completed) continue;

					var remoteEp = new IPEndPoint(IPAddress.Any, 0);
					var data = udp.EndReceive(asyncResult, ref remoteEp);
					var json = Encoding.UTF8.GetString(data);
					var ip = remoteEp.Address.ToString();

					if (discovered.Contains(ip)) continue;

					var obj = JObject.Parse(json);
					var mac = obj["result"]?["mac"]?.ToString() ?? ip;
					var moduleName = obj["result"]?["moduleName"]?.ToString() ?? "WiZ Light";

					var wizData = new WizData(mac, ip, moduleName);
					ControlService.AddDevice(wizData).ConfigureAwait(false);
					discovered.Add(ip);
					Log.Debug("WiZ: Found device at " + ip);
				} catch (SocketException) {
					break;
				} catch (ObjectDisposedException) {
					break;
				} catch (Exception ex) {
					Log.Debug("WiZ: Parse error during discovery: " + ex.Message);
				}
			}
		} catch (Exception e) {
			Log.Warning("WiZ: Discovery exception: " + e.Message);
		}

		Log.Debug("WiZ: Discovery complete.");
	}
}
