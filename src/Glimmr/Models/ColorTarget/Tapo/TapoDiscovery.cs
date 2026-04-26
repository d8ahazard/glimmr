#region

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Tapo;

public class TapoDiscovery : ColorDiscovery, IColorDiscovery {
	private readonly List<string> _discoveredIds;

	private const int BroadcastPort = 20002;

	public TapoDiscovery(ColorService cs) : base(cs) {
		_discoveredIds = new List<string>();
	}

	public async Task Discover(int timeout, CancellationToken ct) {
		_discoveredIds.Clear();
		Log.Debug("Tapo: Discovery started...");

		UdpClient? udp = null;
		try {
			udp = new UdpClient();
			udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
			udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
			udp.EnableBroadcast = true;

			var probe = Encoding.UTF8.GetBytes("{\"system\":{\"get_sysinfo\":null}}");
			await udp.SendAsync(probe, probe.Length, new IPEndPoint(IPAddress.Broadcast, BroadcastPort));

			var deadline = DateTime.UtcNow.AddSeconds(timeout);
			while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested) {
				var receiveTask = udp.ReceiveAsync();
				var remaining = deadline - DateTime.UtcNow;
				if (remaining <= TimeSpan.Zero) break;

				var completed = await Task.WhenAny(receiveTask, Task.Delay(remaining, ct));
				if (completed != receiveTask) break;

				try {
					var result = await receiveTask;
					var ip = result.RemoteEndPoint.Address.ToString();
					var id = "tapo_" + ip.Replace(".", "_");

					if (_discoveredIds.Contains(id)) continue;

					Log.Debug("Tapo: Device found at {Ip}", ip);
					var data = new TapoData(id, ip, "Tapo " + ip);
					var existing = DataUtil.GetDevice<TapoData>(id);
					if (existing != null) {
						data.Username = existing.Username;
						data.Password = existing.Password;
						data.Enable = existing.Enable;
						data.Brightness = existing.Brightness;
					}

					await ControlService.AddDevice(data);
					_discoveredIds.Add(id);
				} catch (Exception ex) {
					Log.Debug("Tapo: Error processing response: " + ex.Message);
				}
			}
		} catch (OperationCanceledException) {
			// Expected
		} catch (Exception ex) {
			Log.Debug("Tapo: Discovery error: " + ex.Message);
		} finally {
			udp?.Dispose();
		}

		Log.Debug("Tapo: Discovery complete.");
	}
}
