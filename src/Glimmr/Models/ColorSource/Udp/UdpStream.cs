#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
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
	public sealed override FrameBuilder? Builder { get; set; }
	public sealed override FrameSplitter Splitter { get; set; }
	private readonly ControlService _cs;
	private readonly CancellationTokenSource _cts;
	private readonly CancellationToken _listenToken;
	private readonly UdpClient _uc;
	private readonly Socket _us;
	private readonly ManualResetEvent allDone;
	private CancellationTokenSource _cancelSource;
	private Task? _cancelTask;
	private DeviceMode _devMode;
	private ServiceDiscovery? _discovery;
	private GlimmrData? _gd;
	private string _hostName;
	private SystemData _sd;
	private bool _sourceActive;
	private int _timeOut;

	public UdpStream(ColorService cs) {
		allDone = new ManualResetEvent(false);
		_cs = cs.ControlService;
		_cs.RefreshSystemEvent += RefreshSystem;
		_cs.SetModeEvent += Mode;
		_cs.StartStreamEvent += StartStream;
		Splitter = new FrameSplitter(cs);
		_uc = new UdpClient(21324) { Ttl = 5, Client = { ReceiveBufferSize = 2000 } };
		_uc.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
		_uc.Client.Blocking = false;
		_us = new Socket(AddressFamily.InterNetwork,
			SocketType.Stream,
			ProtocolType.Tcp);

		// bind the listening socket to the port
		var hostIP = IpUtil.GetLocalIpAddress();
		var ep = new IPEndPoint(IPAddress.Parse(hostIP), 19445);
		_us.Bind(ep);

		_cancelSource = new CancellationTokenSource();
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
			_uc.DontFragment = true;
		}

		_cancelSource = new CancellationTokenSource();


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
		//Task.Run(Listen2, _listenToken);
	}

	public override Task Start(CancellationToken ct) {
		Log.Information("Starting UDP Stream service...");
		Splitter.DoSend = true;
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
		Builder = new FrameBuilder(dims);
		await _cs.SetMode(Udp);
	}

	private Task Mode(object arg1, DynamicEventArgs arg2) {
		_devMode = (DeviceMode)arg2.Arg0;
		return Task.CompletedTask;
	}

	protected override Task ExecuteAsync(CancellationToken stoppingToken) {
		Splitter.DoSend = true;
		return Task.Run(async () => {
			try {
				while (!stoppingToken.IsCancellationRequested) {
					await Task.Delay(1000, stoppingToken);
				}
			} catch (Exception e) {
				Log.Warning("Exception: " + e.Message);
			}

			Splitter.DoSend = false;
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

	private async Task Listen2() {
		_us.Listen(1000);
		while (!_listenToken.IsCancellationRequested) {
			try {
				allDone.Reset();
				_us.BeginAccept(Listen_Callback, _us);
				allDone.WaitOne();
			} catch (Exception) {
				// Ignored
			}
		}

		await Task.FromResult(true);
	}

	private void Listen_Callback(IAsyncResult ar) {
		Log.Debug("Accept callback.");
		allDone.Set();
		if (ar.AsyncState == null) {
			Log.Debug("Null async stage!");
			return;
		}

		var listener = (Socket)ar.AsyncState;
		var handler = listener.EndAccept(ar);
		var state = new StateObject {
			WorkSocket = handler
		};
		handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
			ReadCallback, state);
	}

	private static void ReadCallback(IAsyncResult ar) {
		Log.Debug("Read callback!!");
		if (ar.AsyncState == null) {
			Log.Debug("Null async state!");
			return;
		}

		var state = (StateObject)ar.AsyncState;
		var handler = state.WorkSocket;
		if (handler == null) {
			return;
		}

		// Read data from the client socket.  
		var read = handler.EndReceive(ar);

		// Data was read from the client socket.  
		if (read > 0) {
			for (var i = 0; i <= read; i++) {
				state.sb.AppendFormat("{0:x2}", state.buffer[i]);
			}

			Log.Debug("State (reading): " + state.sb);
			handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
				ReadCallback, state);
		} else {
			if (state.sb.Length > 1) {
				// All the data has been read from the client;  
				// display it on the console.  
				Log.Debug("State (done): " + state.sb);
			}

			handler.Close();
		}
	}

	public override Task StopAsync(CancellationToken stoppingToken) {
		_uc.Close();
		_uc.Dispose();
		_cancelTask?.Dispose();
		return Task.CompletedTask;
	}

	private async Task ProcessFrame(IEnumerable<byte> data) {
		Splitter.DoSend = true;
		_sourceActive = true;

		if (_devMode != Udp) {
			_gd = new GlimmrData(DataUtil.GetSystemData());
			var dims = new[] { _gd.LeftCount, _gd.RightCount, _gd.TopCount, _gd.BottomCount };
			Builder = new FrameBuilder(dims);
			await _cs.SetMode(Udp);
		}

		try {
			var cp = new ColorPacket(data.ToArray());
			var ledColors = cp.Colors;
			if (Builder == null) {
				Log.Warning("Null builder.");
				return;
			}

			// Set our timeout value and restart watch every time a frame is received
			_timeOut = cp.Duration;
			_cancelSource.Cancel();
			_cancelSource = new CancellationTokenSource();
			_cancelTask = new Task(DisableSource, _cancelSource.Token);
			var frame = Builder.Build(ledColors);
			if (frame != null) {
				await Splitter.Update(frame);
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
			Splitter.DoSend = SourceActive;
		} catch (Exception) {
			//ignored
		}
	}

	private class StateObject {
		public const int BufferSize = 1024;
		public readonly byte[] buffer = new byte[BufferSize];
		public readonly StringBuilder sb = new();
		public Socket? WorkSocket;
	}
}