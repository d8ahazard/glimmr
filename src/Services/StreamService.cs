using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using System.Drawing;
using Glimmr.Models;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Glimmr.Services {
	public class StreamService : BackgroundService {
		private readonly ControlService _cs;
		private readonly UdpClient _uc;
		private int _devMode;

		public StreamService(ControlService cs) {
			_cs = cs;
			_cs.RefreshSystemEvent += Refresh;
			_uc = new UdpClient(8889) {Ttl = 5};
			_uc.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
			_uc.Client.Blocking = false;
			_uc.DontFragment = true;
			SystemData sd = DataUtil.GetObject<SystemData>("SystemData");
			_devMode = sd.DeviceMode;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
			
			Log.Debug("Starting udp listener...");
			while(!stoppingToken.IsCancellationRequested) {
				var receivedResult = await _uc.ReceiveAsync();
				var bytes = receivedResult.Buffer;
				await ProcessFrame(bytes);
			}

			Log.Debug("UDP Receiver canceled...");
		}
		
		public override Task StopAsync(CancellationToken stoppingToken) {
			_uc.Close();
			_uc.Dispose();
			return Task.CompletedTask;
		}

		private async Task ProcessFrame(byte[] data) {
			var flag = data[0];
			var time = data[1];
			if (flag != 2) {
				Log.Debug("Flag is invalid.");
			}

			var bytes = data.Skip(2).ToArray();
			var colors = new List<Color>();
			if (_devMode == 5) {
				for (var i = 0; i < bytes.Length; i += 3) {
					if (i + 2 >= bytes.Length) {
						continue;
					}

					var col = Color.FromArgb(255, bytes[i], bytes[i + 1], bytes[i + 2]);
					colors.Add(col);
				}

				var secIdx = colors.Count - 28;
				var ledColors = colors.GetRange(0, secIdx);
				var sectorColors = colors.Skip(secIdx).ToList();
				await _cs.SendColors(ledColors, sectorColors);
			}
		}

		private void Refresh() {
			_devMode = DataUtil.GetItem("DeviceMode");
		}
	}
}