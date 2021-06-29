#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Models;
using Glimmr.Models.ColorTarget.Glimmr;
using Glimmr.Models.Util;
using Makaretu.Dns;
using Microsoft.Extensions.Hosting;
using Serilog;
using static Glimmr.Enums.DeviceMode;

#endregion

namespace Glimmr.Services {
	public class StreamService : BackgroundService {
		private readonly ControlService _cs;
		private readonly ColorService _colorService;
		private readonly UdpClient _uc;
		private int _devMode;
		private int _ledCount;
		private bool _loaded;
		private bool _sending;
		private bool _mirrorHorizontal;
		private SystemData _sd;
		private GlimmrData? _gd;
		private int _sectorCount;
		private bool _useCenter;
		private bool _streaming;
		private int[] tgtDimensions = new int[4];
		private int[] _srcDimensions = new int[4];
		private Dictionary<int, int> _ledMap;
		private Dictionary<int, int> _sectorMap;


		public StreamService(ControlService cs) {
			_cs = cs;
			_colorService = cs.ColorService;
			_cs.RefreshSystemEvent += Refresh;
			_cs.SetModeEvent += Mode;
			_cs.StartStreamEvent += StartStream;
			_cs.RefreshSystemEvent += RefreshSd;
			_uc = new UdpClient(8889) {Ttl = 5};
			_uc.Client.ReceiveBufferSize = 2000;
			_uc.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
			_uc.Client.Blocking = false;
			_uc.DontFragment = true;
			var sd = DataUtil.GetSystemData();
			_devMode = sd.DeviceMode;
			_sd = sd;
			tgtDimensions = new[] {_sd.LeftCount, _sd.RightCount, _sd.TopCount, _sd.BottomCount};
		}

		private void RefreshSd() {
			_sd = DataUtil.GetSystemData();
			tgtDimensions = new[] {_sd.LeftCount, _sd.RightCount, _sd.TopCount, _sd.BottomCount};
		}

		private async Task StartStream(object arg1, DynamicEventArgs arg2) {
			_gd = arg2.P1;
			_sd = DataUtil.GetSystemData();
			_useCenter = _gd.UseCenter;
			_mirrorHorizontal = _gd.MirrorHorizontal;
			_sectorCount = _gd.SectorCount;
			_ledCount = _gd.LedCount;
			_loaded = true;
			_srcDimensions = new[] {_gd.LeftCount, _gd.RightCount, _gd.TopCount, _gd.BottomCount};
			_ledMap = ColorUtil.MapSectors(_sectorCount, new[] {_gd.VCount, _gd.HCount});
			_sectorMap = ColorUtil.MapLeds(_ledCount, _srcDimensions, tgtDimensions);
			_streaming = true;
			await _cs.SetMode(5);
		}

		private Task Mode(object arg1, DynamicEventArgs arg2) {
			_devMode = arg2.P1;
			_streaming = (DeviceMode) _devMode == Streaming; 
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
				Task t1 = Task.Run(() => Listen(),stoppingToken);
				Task t2 = Task.Run(() => Listen(),stoppingToken);
				while (!stoppingToken.IsCancellationRequested) {
					if ((DeviceMode) _devMode != Streaming) {
						await Task.Delay(1, stoppingToken);
					}
				}

				sd.Dispose();
			}, stoppingToken);
		}
		
		private async Task Listen() {
			Log.Debug("Starting listen task...");
			while (true) {
				if (!_streaming) continue;
				try {
					var result = await _uc.ReceiveAsync();
					if (!_sending) await ProcessFrame(result.Buffer).ConfigureAwait(false);
				} catch (Exception ex) {
					// Ignored
				}
			}
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
					var cols = new Color[_sd.LedCount];
					foreach (var (key, value) in _ledMap) {
						cols[key] = colors[value];
					}
					colors = cols;
				}

				if (!_useCenter && _sd.SectorCount != _sectorCount) {
					var secs = new Color[_sd.SectorCount];
					foreach (var (key, value) in _sectorMap) {
						secs[key] = sectors[value];
					}
					sectors = secs;
				}

				var ledColors = colors.ToList();
				var sectorColors = sectors.ToList();
				
				_colorService.SendColors(ledColors, sectorColors,0);
				_sending = false;
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