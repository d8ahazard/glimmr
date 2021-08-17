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
using Glimmr.Models.ColorTarget.Glimmr;
using Glimmr.Models.Util;
using Glimmr.Services;
using Makaretu.Dns;
using Microsoft.Extensions.Hosting;
using Serilog;
using static Glimmr.Enums.DeviceMode;

#endregion

namespace Glimmr.Models.ColorSource.UDP {
	public class UdpStream : BackgroundService, IColorSource {
		private readonly ControlService _cs;
		private readonly FrameSplitter _splitter;
		private readonly UdpClient _uc;
		private FrameBuilder? _builder;
		private DeviceMode _devMode;
		private ServiceDiscovery? _discovery;
		private GlimmrData? _gd;
		private string _hostName;
		private int _ledCount;
		private SystemData _sd;
		private bool _sending;
		private CancellationToken _stoppingToken;
		private bool _streaming;

		public UdpStream(ColorService cs) {
			_cs = cs.ControlService;
			_cs.RefreshSystemEvent += RefreshSystem;
			_cs.SetModeEvent += Mode;
			_cs.StartStreamEvent += StartStream;
			_splitter = cs.Splitter;
			_uc = new UdpClient(8889) {Ttl = 5, Client = {ReceiveBufferSize = 2000}};
			_uc.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
			_uc.Client.Blocking = false;
			_uc.DontFragment = true;
			var sd = DataUtil.GetSystemData();
			_devMode = (DeviceMode) sd.DeviceMode;
			_sd = sd;
			_hostName = _sd.DeviceName;
			if (string.IsNullOrEmpty(_hostName)) {
				_hostName = Dns.GetHostName();
				_sd.DeviceName = _hostName;
				DataUtil.SetSystemData(_sd);
			}

			RefreshSystem();
		}

		public Task ToggleStream(CancellationToken ct) {
			Log.Information("Starting UDP Stream service...");
			_splitter.DoSend = true;
			return ExecuteAsync(ct);
		}

		public void RefreshSystem() {
			var sd = DataUtil.GetSystemData();
			_devMode = (DeviceMode) sd.DeviceMode;
			_hostName = _sd.DeviceName;
			if (SystemUtil.IsRaspberryPi()) return;
			_discovery?.Dispose();
			var addr = new List<IPAddress> {IPAddress.Parse(IpUtil.GetLocalIpAddress())};
			var service = new ServiceProfile(_hostName, "_glimmr._tcp", 8889, addr);
			_discovery = new ServiceDiscovery();
			_discovery.Advertise(service);
		}

		public bool SourceActive => _splitter.SourceActive;

		
		private async Task StartStream(object arg1, DynamicEventArgs arg2) {
			_gd = arg2.Arg0;
			_sd = DataUtil.GetSystemData();
			_ledCount = _gd.LedCount;
			var dims = new[] {_gd.LeftCount, _gd.RightCount, _gd.TopCount, _gd.BottomCount};
			_builder = new FrameBuilder(dims);
			_streaming = true;
			await _cs.SetMode(5);
		}

		private Task Mode(object arg1, DynamicEventArgs arg2) {
			_devMode = (DeviceMode) arg2.Arg0;
			_streaming = _devMode == Udp;
			return Task.CompletedTask;
		}

		protected override Task ExecuteAsync(CancellationToken stoppingToken) {
			_splitter.DoSend = true;
			_stoppingToken = stoppingToken;
			return Task.Run(async () => {
				var l1 = Task.Run(Listen, stoppingToken);
				var l2 = Task.Run(Listen, stoppingToken);
				await Task.WhenAll(l1, l2);
				_splitter.DoSend = false;
				Log.Information("UDP Stream service stopped.");
			}, stoppingToken);
		}
		
		public Color[] GetColors() {
			return _splitter.GetColors();
		}

		public Color[] GetSectors() {
			return _splitter.GetSectors();
		}


		private async Task Listen() {
			while (!_stoppingToken.IsCancellationRequested) {
				if (!_streaming) {
					continue;
				}

				try {
					var result = await _uc.ReceiveAsync();
					if (!_sending) {
						await ProcessFrame(result.Buffer).ConfigureAwait(false);
					}
				} catch (Exception) {
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
			if (_devMode == Udp) {
				var colIdx = 0;
				for (var i = 0; i < bytes.Length; i += 3) {
					if (i + 2 >= bytes.Length) {
						continue;
					}

					var col = Color.FromArgb(255, bytes[i], bytes[i + 1], bytes[i + 2]);
					if (colIdx < _ledCount) {
						colors[colIdx] = col;
					}

					colIdx++;
				}

				var ledColors = colors.ToList();
				if (_builder == null) {
					return;
				}

				var frame = _builder.Build(ledColors);
				Log.Debug("Update: Udp");
				_splitter.Update(frame);
				_sending = false;
				await Task.FromResult(true);
			} else {
				Log.Debug("Dev mode is incorrect.");
			}
		}
	}
}