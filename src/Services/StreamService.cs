#region

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models;
using Glimmr.Models.ColorTarget.Glimmr;
using Glimmr.Models.Util;
using Makaretu.Dns;
using Microsoft.Extensions.Hosting;
using Serilog;

#endregion

namespace Glimmr.Services {
	public class StreamService : BackgroundService {
		private readonly ControlService _cs;
		private readonly UdpClient _uc;
		private int _devMode;
		private int _ledCount;
		private bool _loaded;
		private bool _mirrorHorizontal;
		private SystemData _sd;
		private int _sectorCount;
		private bool _useCenter;


		public StreamService(ControlService cs) {
			_cs = cs;
			_cs.RefreshSystemEvent += Refresh;
			_cs.SetModeEvent += Mode;
			_cs.StartStreamEvent += StartStream;
			_cs.RefreshSystemEvent += RefreshSd;
			_uc = new UdpClient(8889) {Ttl = 5};
			_uc.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
			_uc.Client.Blocking = false;
			_uc.DontFragment = true;
			var sd = DataUtil.GetSystemData();
			_devMode = sd.DeviceMode;
			_sd = sd;
		}

		private void RefreshSd() {
			_sd = DataUtil.GetSystemData();
		}

		private async Task StartStream(object arg1, DynamicEventArgs arg2) {
			GlimmrData gd = arg2.P1;
			_sd = DataUtil.GetSystemData();
			_useCenter = gd.UseCenter;
			_mirrorHorizontal = gd.MirrorHorizontal;
			_sectorCount = gd.SectorCount;
			_ledCount = gd.LedCount;
			_loaded = true;

			await _cs.SetMode(5);
		}

		private Task Mode(object arg1, DynamicEventArgs arg2) {
			_devMode = arg2.P1;
			return Task.CompletedTask;
		}

		protected override Task ExecuteAsync(CancellationToken stoppingToken) {
			Log.Information("Stream service initialized.");
			return Task.Run(async () => {
				var hostname = Dns.GetHostName();
				var addr = new List<IPAddress> {IPAddress.Parse(IpUtil.GetLocalIpAddress())};
				var service = new ServiceProfile(hostname, "_glimmr._tcp", 8889, addr);
				var sd = new ServiceDiscovery();
				sd.Advertise(service);
				while (!stoppingToken.IsCancellationRequested) {
					if (_devMode != 5) {
						await Task.Delay(1, stoppingToken);
					} else {
						if (!_loaded) {
							continue;
						}

						var receivedResult = await _uc.ReceiveAsync();
						var bytes = receivedResult.Buffer;
						await ProcessFrame(bytes);
					}
				}

				sd.Dispose();
			}, stoppingToken);
		}

		public override Task StopAsync(CancellationToken stoppingToken) {
			_uc.Close();
			_uc.Dispose();
			return Task.CompletedTask;
		}

		private async Task ProcessFrame(IReadOnlyList<byte> data) {
			var flag = data[0];
			if (flag != 2) {
				Log.Warning("Flag is invalid!");
			}

			var bytes = data.Skip(2).ToArray();
			var colors = new Color[_ledCount];
			var sectors = new Color[_sectorCount];
			if (_devMode == 5) {
				var colIdx = 0;
				for (var i = 0; i < bytes.Length; i += 3) {
					if (i + 2 >= bytes.Length) {
						continue;
					}

					var col = Color.FromArgb(255, bytes[i], bytes[i + 1], bytes[i + 2]);
					if (colIdx < _ledCount) {
						colors[colIdx] = col;
					} else {
						var sIdx = colIdx - _ledCount;
						sectors[sIdx] = col;
					}

					colIdx++;
				}

				if (_sd.LedCount != _ledCount) {
					//colors = ColorUtil.ResizeColors(colors, _dimensions).ToArray();
				}

				if (!_useCenter && _sd.SectorCount != _sectorCount) {
					//sectors = ColorUtil.ResizeSectors(sectors, _sectorDimensions).ToArray();
				}

				var ledColors = colors.ToList();
				var sectorColors = sectors.ToList();
				if (_mirrorHorizontal) {
					sectorColors.Reverse();
					ledColors.Reverse();
				}

				_cs.SendColors(ledColors, sectorColors);
				await Task.FromResult(true);
			} else {
				Log.Debug("Dev mode is incorrect.");
			}
		}

		private void Refresh() {
			_devMode = DataUtil.GetItem("DeviceMode");
			var sd = DataUtil.GetSystemData();
			_devMode = sd.DeviceMode;
		}
	}
}