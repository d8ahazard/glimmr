#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Models.ColorTarget.Glimmr;
using Glimmr.Models.Util;
using Glimmr.Services;
using Makaretu.Dns;
using Serilog;
using static Glimmr.Enums.DeviceMode;

#endregion

namespace Glimmr.Models.ColorSource.UDP;

public class UdpStream : ColorSource {
	public override bool SourceActive => _sourceActive;
	private readonly ControlService _cs;
	private readonly CancellationTokenSource _cts;
	private readonly CancellationToken _listenToken;
	private readonly FrameSplitter _splitter;
	private readonly UdpClient _uc;
	private FrameBuilder? _builder;
	private DeviceMode _devMode;
	private ServiceDiscovery? _discovery;
	private GlimmrData? _gd;
	private string _hostName;
	private SystemData _sd;
	private CancellationTokenSource _cancelSource;
	private bool _sourceActive;
	private int _timeOut;
	private Task? _cancelTask;

	public UdpStream(ColorService cs) {
		_cs = cs.ControlService;
		_cs.RefreshSystemEvent += RefreshSystem;
		_cs.SetModeEvent += Mode;
		_cs.StartStreamEvent += StartStream;
		_splitter = new FrameSplitter(cs);
		_uc = new UdpClient(21324) { Ttl = 5, Client = { ReceiveBufferSize = 2000 } };
		_uc.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
		_uc.Client.Blocking = false;
		_cancelSource = new CancellationTokenSource();
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
			_uc.DontFragment = true;
		}

		var sd = DataUtil.GetSystemData();
		_devMode = sd.DeviceMode;
		_sd = sd;
		_hostName = _sd.DeviceName;
		if (string.IsNullOrEmpty(_hostName)) {
			_hostName = Dns.GetHostName();
			_sd.DeviceName = _hostName;
			DataUtil.SetSystemData(_sd);
		}

		RefreshSystem();
		_cts = new CancellationTokenSource();
		_listenToken = _cts.Token;
		Task.Run(Listen, _listenToken);
	}

	public override Task Start(CancellationToken ct) {
		Log.Information("Starting UDP Stream service...");
		_splitter.DoSend = true;
		RunTask = ExecuteAsync(ct);
		return Task.CompletedTask;
	}

	public sealed override void RefreshSystem() {
		var sd = DataUtil.GetSystemData();
		_devMode = sd.DeviceMode;
		_hostName = _sd.DeviceName;
		_discovery?.Dispose();
		var address = new List<IPAddress> { IPAddress.Parse(IpUtil.GetLocalIpAddress()) };
		var service = new ServiceProfile(_hostName, "_glimmr._tcp", 21324, address);
		service.AddProperty("mac", sd.DeviceId);
		_discovery = new ServiceDiscovery();
		_discovery.Advertise(service);
	}


	private async Task StartStream(object arg1, DynamicEventArgs arg2) {
		_gd = arg2.Arg0;
		_sd = DataUtil.GetSystemData();
		var dims = new[] { _gd.LeftCount, _gd.RightCount, _gd.TopCount, _gd.BottomCount };
		_builder = new FrameBuilder(dims);
		await _cs.SetMode(Udp);
	}

	private Task Mode(object arg1, DynamicEventArgs arg2) {
		_devMode = (DeviceMode)arg2.Arg0;
		return Task.CompletedTask;
	}

	protected override Task ExecuteAsync(CancellationToken stoppingToken) {
		_splitter.DoSend = true;
		return Task.Run(async () => {
			try {
				while (!stoppingToken.IsCancellationRequested) {
					await Task.Delay(1000, stoppingToken);
				}
			} catch (Exception e) {
				Log.Warning("Exception: " + e.Message);
			}

			_splitter.DoSend = false;
			_cts.Cancel();
			Log.Information("UDP Stream service stopped.");
		}, stoppingToken);
	}

	private async Task Listen() {
		while (!_listenToken.IsCancellationRequested) {
			try {
				var result = await _uc.ReceiveAsync(_listenToken);
				await ProcessFrame(result.Buffer).ConfigureAwait(false);
			} catch (Exception) {
				// Ignored
			}
		}
	}
	
	public override Task StopAsync(CancellationToken stoppingToken) {
		_uc.Close();
		_uc.Dispose();
		_cancelTask?.Dispose();
		return Task.CompletedTask;
	}

	private async Task ProcessFrame(IEnumerable<byte> data) {
		_splitter.DoSend = true;
		_sourceActive = true;
		
		if (_devMode != Udp) {
			_gd = new GlimmrData(DataUtil.GetSystemData());
			var dims = new[] { _gd.LeftCount, _gd.RightCount, _gd.TopCount, _gd.BottomCount };
			_builder = new FrameBuilder(dims);
			await _cs.SetMode(Udp);
		}

		try {
			var cp = new ColorPacket(data.ToArray());
			var ledColors = cp.Colors;
			if (_builder == null) {
				Log.Warning("Null builder.");
				return;
			}
			
			// Set our timeout value and restart watch every time a frame is received
			_timeOut = cp.Duration;
			_cancelSource.Cancel();
			_cancelSource = new CancellationTokenSource();
			_cancelTask = new Task(DisableSource, _cancelSource.Token);
			var frame = _builder.Build(ledColors);
			if (frame != null) {
				await _splitter.Update(frame);
				frame.Dispose();
			}
		} catch (Exception e) {
			Log.Warning("Exception parsing packet: " + e.Message);
		}
	}

	private async void DisableSource() {
		try {
			await Task.Delay(TimeSpan.FromSeconds(_timeOut), _cancelSource.Token);
			_sourceActive = false;
			_splitter.DoSend = SourceActive;
		} catch (Exception) {
			//ignored
		}
	}
}