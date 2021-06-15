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

namespace Glimmr.Services {
	public class StreamService : BackgroundService {
		private readonly ControlService _cs;
		private readonly UdpClient _uc;
		private int _devMode;
		private int _sectorCount;
		private int _ledCount;
		private int[] _dimensions;
		private int[] _sectorDimensions;
		private bool _useCenter;
		private bool _mirrorHorizontal;
		private SystemData _sd;


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
			_sectorCount = (sd.HSectors + sd.VSectors) * 2 - 4;
		}

		private void RefreshSd() {
			_sd = DataUtil.GetSystemData();
		}

		private async Task StartStream(object arg1, DynamicEventArgs arg2) {
			GlimmrData gd = arg2.P1;
			_dimensions = new[] {gd.RightCount, gd.TopCount, gd.LeftCount, gd.BottomCount};
			_sectorDimensions = new[] {gd.VCount, gd.HCount, gd.VCount, gd.HCount};
			_useCenter = gd.UseCenter;
			_mirrorHorizontal = gd.MirrorHorizontal;
			if (_useCenter) {
				_sectorCount = gd.HCount * gd.VCount;
			} else {
				_sectorCount = gd.HCount + gd.HCount + gd.VCount + gd.VCount - 4;	
			}

			_ledCount = gd.LedCount;
			
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
			var colors = new List<Color>();
			if (_devMode == 5) {
				for (var i = 0; i < bytes.Length; i += 3) {
					if (i + 2 >= bytes.Length) {
						continue;
					}

					var col = Color.FromArgb(255, bytes[i], bytes[i + 1], bytes[i + 2]);
					colors.Add(col);
				}

				var secIdx = colors.Count - _sectorCount;
				var ledColors = colors.GetRange(0, secIdx);
				var sectorColors = colors.Skip(secIdx).ToList();
				if (_sd.LedCount != _ledCount) ledColors = ColorUtil.ResizeColors(ledColors.ToArray(), _dimensions).ToList();
				if (!_useCenter && !_sd.UseCenter) {
					sectorColors = ColorUtil.ResizeColors(sectorColors.ToArray(), _sectorDimensions).ToList();
				}

				if (_mirrorHorizontal) {
					sectorColors.Reverse();
					ledColors.Reverse();
				}
				_cs.SendColors(ledColors, sectorColors);
				await Task.FromResult(true);
			}
		}

		private void Refresh() {
			_devMode = DataUtil.GetItem("DeviceMode");
			var sd = DataUtil.GetSystemData();
			_devMode = sd.DeviceMode;
			_sectorCount = (sd.HSectors + sd.VSectors) * 2 - 4;
		}
	}
}