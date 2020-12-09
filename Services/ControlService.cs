using System;
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
using Color = System.Drawing.Color;

namespace Glimmr.Services {
	public class ControlService : BackgroundService {

		private IHubContext<SocketServer> _hubContext;
		public LifxClient LifxClient { get; }
		public HttpClient NanoClient { get; }
		public Socket NanoSocket { get; }


		public ControlService(IHubContext<SocketServer> hubContext) {
			_hubContext = hubContext;
			// Lifx client
			LifxClient = LifxClient.CreateAsync().Result;
			// Init nano HttpClient
			NanoClient = new HttpClient();
            
			// Init nano socket
			NanoSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			NanoSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
			NanoSocket.EnableBroadcast = false;
		}

		public event ArgUtils.Action RescanDeviceEvent = delegate { };
		public event Action<string> DeviceReloadEvent = delegate { };

		public event Action RefreshLedEvent = delegate { };

		public event Action DeviceRescanEvent = delegate { };
		public event ArgUtils.Action DreamSubscribeEvent = delegate { };
		public event Action<int> SetModeEvent = delegate { };
		public event Action<int, bool, int> TestLedEvent = delegate { };

		public event Action<CancellationToken> RefreshDreamScreenEvent = delegate { };
		public event Action<int> SetAmbientModeEvent = delegate { };
		public event Action<int> SetAmbientShowEvent = delegate { };
		public event Action<Color, string, int> SetAmbientColorEvent = delegate { };
		public event Action<string, dynamic, string> SendDreamMessageEvent = delegate { };

		public event Action<int, int, byte[], byte, byte, IPEndPoint, bool>  SendUdpWriteEvent = delegate { };

		public event Action<List<Color>, string, int> SendSectorsEvent = delegate { };
		public event Action<int> SetCaptureModeEvent = delegate { };
		public event Action<List<Color>> TriggerSendColorsEventDs = delegate { };
		public event Action<List<Color>, List<Color>> TriggerSendColorsEvent = delegate { };
		public event Action<List<Color>, List<Color>, List<Color>> TriggerSendColorsEvent2= delegate { };



		public void ScanDevices() {
			RescanDeviceEvent();
		}

		public void SetMode(int mode) {
			LogUtil.Write("Setting mode: " + mode);
			_hubContext.Clients.All.SendAsync("mode", mode);
			DataUtil.SetItem<int>("DeviceMode",mode);
			SetModeEvent(mode);
		}

		public void TestLeds(int len, bool stop, int test) {
			TestLedEvent(len, stop, test);
		}
		
		public void ResetMode() {
			var curMode = DataUtil.GetItem("DeviceMode");
			if (curMode == 0) return;
			SetMode(0);
			Thread.Sleep(1000);
			SetMode(curMode);
		}

		
		public void SetCaptureMode(int mode) {
			_hubContext.Clients.All.SendAsync("captureMode", mode);
			DataUtil.SetItem<int>("CaptureMode",mode);
			SetCaptureModeEvent(mode);
		}
		
		public void SetAmbientMode(int mode) {
			_hubContext.Clients.All.SendAsync("ambientMode", mode);
			DataUtil.SetItem<int>("AmbientMode",mode);
			SetAmbientModeEvent(mode);
		}
		
		public void SetAmbientShow(int show) {
			_hubContext.Clients.All.SendAsync("ambientShow", show);
			DataUtil.SetItem<int>("AmbientShow",show);
			SetAmbientShowEvent(show);
		}

		public void SetAmbientColor(Color color, string id, int group) {
			_hubContext.Clients.All.SendAsync("ambientColor", color);
			DataUtil.SetItem<int>("AmbientColor",color);
			SetAmbientColorEvent(color, id, group);
		}

		public void SendDreamMessage(string command, dynamic message, string id) {
			SendDreamMessageEvent(command, message, id);
		}

		public void SendSectors(List<Color> colors, string id, int group) {
			SendSectorsEvent(colors, id, group);
		}

		/// <summary>
		/// Call this to trigger device refresh
		/// </summary>
		public void RefreshDevice(string id) {
			DeviceReloadEvent(id);
		}

		public void RescanDevices() {
			DeviceRescanEvent();
		}

		public void RefreshLedData() {
			RefreshLedEvent();
		}

		public void SendColors(List<Color> c1) {
			TriggerSendColorsEventDs(c1);
		}
		
		public void SendColors(List<Color> c1, List<Color> c2) {
			TriggerSendColorsEvent(c1, c2);
		}

		public void SendColors(List<Color> colors, List<Color> sectors, List<Color> sectorsV2) {
			TriggerSendColorsEvent2(colors, sectors, sectorsV2);
		}
		
		public void TriggerDreamSubscribe() {
			DreamSubscribeEvent();
		}
		
		
		public async void NotifyClients() {
			await _hubContext.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
		}

		protected override Task ExecuteAsync(CancellationToken stoppingToken) {
			return Task.Run(async () => {
				var curMode = DataUtil.GetItem("DeviceMode");
				SetModeEvent(curMode);

				while (!stoppingToken.IsCancellationRequested) {
					await Task.Delay(1, stoppingToken);	
				}
				LifxClient?.Dispose();
				NanoClient?.Dispose();
				NanoSocket?.Dispose();
			});
		}


		public void SendUdpWrite(int p0, int p1, byte[] p2, byte mFlag, byte groupNumber, IPEndPoint p5, bool groupSend = false) {
			SendUdpWriteEvent(p0, p1, p2, mFlag, groupNumber, p5, groupSend);
		}

		public void RefreshDreamScreen(in CancellationToken csToken) {
			RefreshDreamScreenEvent(csToken);
		}

		
	}
}