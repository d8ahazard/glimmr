﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging.Configuration;
using Glimmr.Hubs;
using Glimmr.Models.Util;
using LifxNet;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Serilog;
using Color = System.Drawing.Color;

namespace Glimmr.Services {
	public class ControlService : BackgroundService {
		public HttpClient HttpSender { get; }
		public LifxClient LifxClient { get; }
		public UdpClient UdpClient { get; }
		private readonly IHubContext<SocketServer> _hubContext;

		public ControlService(IHubContext<SocketServer> hubContext) {
			_hubContext = hubContext;
			// Lifx client
			LifxClient = LifxClient.CreateAsync().Result;
			// Init nano HttpClient
			HttpSender = new HttpClient();
			DataUtil.CheckDefaults(LifxClient);
			// Init UDP clients

			UdpClient = new UdpClient {Ttl = 128};
			UdpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
			UdpClient.Client.Blocking = false;
			UdpClient.DontFragment = true;
		}

		public event Action<string> DeviceReloadEvent = delegate { };

		public event Action RefreshLedEvent = delegate { };

		public event Action DeviceRescanEvent = delegate { };
		public event ArgUtils.Action DreamSubscribeEvent = delegate { };
		public event Action<int> SetModeEvent = delegate { };
		public event Action<int, bool, int> TestLedEvent = delegate { };
		public event Action<CancellationToken> RefreshDreamscreenEvent = delegate { };
		public event Action<string> AddSubscriberEvent = delegate { };
		public event Action<int> SetAmbientModeEvent = delegate { };
		public event Action<int> SetAmbientShowEvent = delegate { };
		public event Action<Color, string, int> SetAmbientColorEvent = delegate { };
		public event Action<string, dynamic, string> SendDreamMessageEvent = delegate { };
		public event Action<int, int, byte[], byte, byte, IPEndPoint, bool> SendUdpWriteEvent = delegate { };
		public event Action<int> SetCaptureModeEvent = delegate { };
		public event Action<List<Color>, List<Color>, int> TriggerSendColorsEvent = delegate { };
		public event Action<List<Color>, List<Color>, int> TriggerSendColorsEvent2 = delegate { };


		public void ScanDevices() {
			DeviceRescanEvent();
		}

		public void SetMode(int mode) {
			Log.Information("Setting mode: " + mode);
			_hubContext.Clients.All.SendAsync("mode", mode);
			DataUtil.SetItem<int>("DeviceMode", mode);
			SetModeEvent(mode);
		}

		public void TestLeds(int len, bool stop, int test) {
			TestLedEvent(len, stop, test);
		}

		public void AddSubscriber(string ip) {
			AddSubscriberEvent(ip);
		}

		public void ResetMode() {
			var curMode = DataUtil.GetItem("DeviceMode");
			if (curMode == 0) {
				return;
			}

			SetMode(0);
			Thread.Sleep(1000);
			SetMode(curMode);
		}


		public void SetCaptureMode(int mode) {
			_hubContext.Clients.All.SendAsync("captureMode", mode);
			DataUtil.SetItem<int>("CaptureMode", mode);
			SetCaptureModeEvent(mode);
		}

		public void SetAmbientMode(int mode) {
			_hubContext.Clients.All.SendAsync("ambientMode", mode);
			DataUtil.SetItem<int>("AmbientMode", mode);
			SetAmbientModeEvent(mode);
		}

		public void SetAmbientShow(int show) {
			_hubContext.Clients.All.SendAsync("ambientShow", show);
			DataUtil.SetItem<int>("AmbientShow", show);
			SetAmbientShowEvent(show);
		}

		public void SetAmbientColor(Color color, string id, int group) {
			_hubContext.Clients.All.SendAsync("ambientColor", color);
			DataUtil.SetObject<Color>("AmbientColor", color);
			var myDev = DataUtil.GetDeviceData();
			myDev.AmbientColor = ColorUtil.ColorToHex(color);
			DataUtil.SetDeviceData(myDev);
			SetAmbientColorEvent(color, id, group);
		}

		public void SendDreamMessage(string command, dynamic message, string id) {
			SendDreamMessageEvent(command, message, id);
		}


		/// <summary>
		///     Call this to trigger device refresh
		/// </summary>
		public void RefreshDevice(string id) {
			DeviceReloadEvent(id);
		}

		public void RescanDevices() {
			Log.Debug("Triggering rescan.");
			DeviceRescanEvent();
		}

		public void RefreshLedData() {
			RefreshLedEvent();
		}

		// We call this one to send colors to everything, including the color service
		public void SendColors(List<Color> c1, List<Color> c2, int fadeTime = 0) {
			TriggerSendColorsEvent(c1, c2, fadeTime);
		}

		// We call this one to send colors to everything except the color service
		public void SendColors2(List<Color> c1, List<Color> c2, int fadeTime = 0) {
			TriggerSendColorsEvent2(c1, c2, fadeTime);
		}

		public void TriggerDreamSubscribe() {
			DreamSubscribeEvent();
		}


		public async void NotifyClients() {
			await _hubContext.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
		}

		protected override Task ExecuteAsync(CancellationToken stoppingToken) {
			return Task.Run(async () => {
				while (!stoppingToken.IsCancellationRequested) {
					await Task.Delay(1, stoppingToken);
				}
				return Task.CompletedTask;
			}, stoppingToken);
		}

		public override Task StopAsync(CancellationToken cancellationToken) {
			Log.Debug("Stopping control service...");
			LifxClient?.Dispose();
			HttpSender?.Dispose();
			UdpClient?.Dispose();
			UdpClient?.Dispose();
			Log.Debug("Control service stopped.");
			return base.StopAsync(cancellationToken);
		}


		public void SendUdpWrite(int p0, int p1, byte[] p2, byte mFlag, byte groupNumber, IPEndPoint p5,
			bool groupSend = false) {
			SendUdpWriteEvent(p0, p1, p2, mFlag, groupNumber, p5, groupSend);
		}

		public void RefreshDreamscreen(in CancellationToken csToken) {
			RefreshDreamscreenEvent(csToken);
		}
	}
}