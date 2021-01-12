using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging.Configuration;
using Glimmr.Hubs;
using Glimmr.Models;
using Glimmr.Models.LED;
using Glimmr.Models.StreamingDevice.Dreamscreen;
using Glimmr.Models.StreamingDevice.Hue;
using Glimmr.Models.StreamingDevice.LIFX;
using Glimmr.Models.StreamingDevice.Nanoleaf;
using Glimmr.Models.StreamingDevice.WLED;
using Glimmr.Models.Util;
using LifxNet;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using Color = System.Drawing.Color;

namespace Glimmr.Services {
	public class ControlService : BackgroundService {
		public HttpClient HttpSender { get; }
		public LifxClient LifxClient { get; }
		public UdpClient UdpClient { get; }
		public readonly IHubContext<SocketServer> HubContext;

		public ControlService(IHubContext<SocketServer> hubContext) {
			HubContext = hubContext;
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
		public event Action<string> FlashDeviceEvent = delegate { };
		public event Action<int> FlashSectorEvent = delegate { };
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
			HubContext.Clients.All.SendAsync("mode", mode);
			DataUtil.SetItem<int>("DeviceMode", mode);
			SetModeEvent(mode);
		}

		public async void AuthorizeHue(string id) {
			Log.Debug("AuthHue called, for real (socket): " + id);
			HueData bd;
			if (!string.IsNullOrEmpty(id)) {
				await HubContext.Clients.All.SendAsync("hueAuth", "start");
				bd = DataUtil.GetCollectionItem<HueData>("Dev_Hue", id);
				Log.Debug("BD: " + JsonConvert.SerializeObject(bd));
				if (bd == null) {
					Log.Debug("Null bridge retrieved.");
					await HubContext.Clients.All.SendAsync("hueAuth", "stop");
					return;
				}

				if (bd.Key != null && bd.User != null) {
					Log.Debug("Bridge is already authorized.");
					await HubContext.Clients.All.SendAsync("hueAuth", "authorized");
					await HubContext.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
					return;
				}
			} else {
				Log.Warning("Null value.");
				await HubContext.Clients.All.SendAsync("hueAuth", "stop");
				return;
			}

			Log.Debug("Trying to retrieve appkey...");
			var count = 0;
			while (count < 30) {
				count++;
				try {
					var appKey = HueDiscovery.CheckAuth(bd.IpAddress).Result;
					Log.Debug("App key retrieved! " + JsonConvert.SerializeObject(appKey));
					if (appKey != null) {
						if (!string.IsNullOrEmpty(appKey.StreamingClientKey)) {
							Log.Debug("Updating bridge?");
							bd.Key = appKey.StreamingClientKey;
							bd.User = appKey.Username;
							Log.Debug("Creating new bridge...");
							// Need to grab light group stuff here
							var nhb = new HueDevice(bd);
							bd = nhb.RefreshData().Result;
							nhb.Dispose();
							DataUtil.InsertCollection<HueData>("Dev_Hue", bd);
							await HubContext.Clients.All.SendAsync("hueAuth", "authorized");
							await HubContext.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
							return;
						}

						Log.Debug("Appkey is null?");
					}

					Log.Debug("Waiting for app key.");
				} catch (NullReferenceException e) {
					Log.Error("Null exception.", e);
				}

				await HubContext.Clients.All.SendAsync("hueAuth", count);
				Thread.Sleep(1000);
			}

			Log.Debug("We should be authorized, returning.");
		}

		public void TriggerImageUpdate() {
			HubContext.Clients.All.SendAsync("loadPreview");
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
			HubContext.Clients.All.SendAsync("captureMode", mode);
			DataUtil.SetItem<int>("CaptureMode", mode);
			SetCaptureModeEvent(mode);
		}

		public void SetAmbientMode(int mode, string deviceId = null) {
			HubContext.Clients.All.SendAsync("ambientMode", mode, deviceId);
			if (deviceId == "127.0.0.1" || deviceId == null) {
				DataUtil.SetItem<int>("AmbientMode", mode);
				SetAmbientModeEvent(mode);	
			} else {
				// Send dream message changing ambient mode
			}
			
		}

		public void SetAmbientShow(int show, string deviceId = null) {
			HubContext.Clients.All.SendAsync("ambientShow", show, deviceId);
			if (deviceId == "127.0.0.1" || deviceId == null) {
				DataUtil.SetItem<int>("AmbientShow", show);
				SetAmbientShowEvent(show);	
			} else {
				// Send dream message
			}
		}

		public void SetAmbientColor(Color color, string id, int group) {
			HubContext.Clients.All.SendAsync("ambientColor", color);
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
			await HubContext.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
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

		public async void AuthorizeNano(string id) {
			var leaf = DataUtil.GetCollectionItem<NanoleafData>("Dev_Nano", id);
			bool doAuth = leaf.Token == null;
			if (doAuth) {
				await HubContext.Clients.All.SendAsync("nanoAuth", "authorized");
				await HubContext.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
				return;
			}

			var panel = new NanoleafDevice(leaf, HttpSender);
			var count = 0;
			while (count < 30) {
				var appKey = panel.CheckAuth().Result;
				if (appKey != null) {
					leaf.Token = appKey.Token;
					DataUtil.InsertCollection<NanoleafData>("Dev_Nanoleaf", leaf);
					await HubContext.Clients.All.SendAsync("nanoAuth", "authorized");
					await HubContext.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
					panel.Dispose();
					return;
				}

				await HubContext.Clients.All.SendAsync("nanoAuth", count);
				Thread.Sleep(1000);
				count++;
			}

			await HubContext.Clients.All.SendAsync("nanoAuth", "stop");

			panel.Dispose();
		}

		public void UpdateLed(LedData ld) {
			Log.Debug("Got LD from post: " + JsonConvert.SerializeObject(ld));
			DataUtil.SetObject<LedData>("LedData", ld);
			RefreshLedData();
			NotifyClients();
		}

		public void UpdateSystem(SystemData sd) {
			DataUtil.SetObject<SystemData>("SystemData", sd);
		}

		public void SystemControl(string action) {
			Log.Debug("Action triggered: " + action);
			switch (action) {
				case "restart":
					SystemUtil.Restart();
					break;
				case "shutdown":
					SystemUtil.Shutdown();
					break;
				case "reboot":
					SystemUtil.Reboot();
					break;
				case "update":
					SystemUtil.Update();
					break;
			}
		}

		public void UpdateDevice(JObject device) {
			Log.Debug("Update device called!");
			var tag = (string) device.GetValue("Tag");
			var id = (string) device.GetValue("_id");
			device["Id"] = id;
			Log.Debug($"ID and tag are {id} and {tag}.");
			var updated = false;
			try {
				switch (tag) {
					case "Wled":
						DataUtil.InsertCollection<WledData>("Dev_Wled", device.ToObject<WledData>());
						updated = true;
						break;
					case "Lifx":
						DataUtil.InsertCollection<LifxData>("Dev_Lifx", device.ToObject<LifxData>());
						updated = true;
						break;
					case "HueBridge":
						var dev = device.ToObject<HueData>();
						DataUtil.InsertCollection<HueData>("Dev_Hue", dev);
						updated = true;
						break;
					case "Nanoleaf":
						DataUtil.InsertCollection<NanoleafData>("Dev_Nanoleaf", device.ToObject<NanoleafData>());
						updated = true;
						break;
					case "Dreamscreen":
						DataUtil.InsertCollection<DreamData>("Dev_Dreamscreen", device.ToObject<DreamData>());
						updated = true;
						break;
					default:
						Log.Debug("Unknown tag: " + tag);
						break;
				}
			} catch (Exception e) {
				Log.Debug("Well, this is exceptional: " + e.Message);
			}

			if (updated) {
				Log.Debug("Triggering device refresh for " + id);
				RefreshDevice(id);
			} else {
				Log.Debug("Sigh, no update...");
			}
		}

		public void FlashDevice(string deviceId) {
			Log.Debug("We got us some flashin' to do.");
			FlashDeviceEvent(deviceId);
		}

		public void FlashSector(in int sector) {
			FlashSectorEvent(sector);
		}
	}
}