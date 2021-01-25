using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging.Configuration;
using Glimmr.Hubs;
using Glimmr.Models;
using Glimmr.Models.ColorTarget.DreamScreen;
using Glimmr.Models.ColorTarget.Hue;
using Glimmr.Models.ColorTarget.LED;
using Glimmr.Models.ColorTarget.LIFX;
using Glimmr.Models.ColorTarget.Nanoleaf;
using Glimmr.Models.ColorTarget.Wled;
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
		private readonly IHubContext<SocketServer> _hubContext;

		public ControlService(IHubContext<SocketServer> hubContext) {
			_hubContext = hubContext;
			// Lifx client
			LifxClient = LifxClient.CreateAsync().Result;
			// Init nano HttpClient
			HttpSender = new HttpClient();
			DataUtil.CheckDefaults(LifxClient).ConfigureAwait(true);
			// Init UDP client

			UdpClient = new UdpClient {Ttl = 5};
			UdpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
			UdpClient.Client.Blocking = false;
			UdpClient.DontFragment = true;
		}

		public AsyncEvent<DynamicEventArgs> DeviceReloadEvent;
		public event Action RefreshLedEvent = delegate { };
		public event Action RefreshSystemEvent = delegate { };
		public AsyncEvent<DynamicEventArgs> DeviceRescanEvent;
		public event ArgUtils.Action DreamSubscribeEvent = delegate { };
		
		public AsyncEvent<DynamicEventArgs> SetModeEvent;

		public AsyncEvent<DynamicEventArgs> TestLedEvent;
		public event Action<CancellationToken> RefreshDreamscreenEvent = delegate { };
		public event Action<string> AddSubscriberEvent = delegate { };
		
		public AsyncEvent<DynamicEventArgs> FlashDeviceEvent;
		
		public AsyncEvent<DynamicEventArgs> FlashSectorEvent;
		
		public AsyncEvent<DynamicEventArgs> DemoLedEvent;
		
		public AsyncEvent<DynamicEventArgs> TriggerSendColorEvent;
		

		public async Task ScanDevices() {
			await DeviceRescanEvent.InvokeAsync(this,null);
		}

		public async Task SetMode(int mode) {
			Log.Information("Setting mode: " + mode);
			await _hubContext.Clients.All.SendAsync("mode", mode);
			DataUtil.SetItem("DeviceMode", mode);
			await SetModeEvent.InvokeAsync(null, new DynamicEventArgs(mode));
		}

		public async Task AuthorizeHue(string id) {
			Log.Debug("AuthHue called, for real (socket): " + id);
			HueData bd;
			if (!string.IsNullOrEmpty(id)) {
				await _hubContext.Clients.All.SendAsync("hueAuth", "start");
				bd = DataUtil.GetCollectionItem<HueData>("Dev_Hue", id);
				Log.Debug("BD: " + JsonConvert.SerializeObject(bd));
				if (bd == null) {
					Log.Debug("Null bridge retrieved.");
					await _hubContext.Clients.All.SendAsync("hueAuth", "stop");
					return;
				}

				if (bd.Key != null && bd.User != null) {
					Log.Debug("Bridge is already authorized.");
					await _hubContext.Clients.All.SendAsync("hueAuth", "authorized");
					await _hubContext.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
					return;
				}
			} else {
				Log.Warning("Null value.");
				await _hubContext.Clients.All.SendAsync("hueAuth", "stop");
				return;
			}

			Log.Debug("Trying to retrieve app key...");
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
							await DataUtil.InsertCollection<HueData>("Dev_Hue", bd);
							await _hubContext.Clients.All.SendAsync("hueAuth", "authorized");
							await _hubContext.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
							return;
						}

						Log.Debug("App key is null?");
					}

					Log.Debug("Waiting for app key.");
				} catch (NullReferenceException) {
					Log.Error("Null exception.");
				}

				await _hubContext.Clients.All.SendAsync("hueAuth", count);
				Thread.Sleep(1000);
			}

			Log.Debug("We should be authorized, returning.");
		}

		public void TriggerImageUpdate() {
			_hubContext.Clients.All.SendAsync("loadPreview");
		}

		public async Task TestLights(int led) {
			await TestLedEvent.InvokeAsync(this, new DynamicEventArgs(led));
		}

		public void AddSubscriber(string ip) {
			AddSubscriberEvent(ip);
		}


		/// <summary>
		///     Call this to trigger device refresh
		/// </summary>
		public async Task RefreshDevice(string id) {
			await DeviceReloadEvent.InvokeAsync(this, new DynamicEventArgs(id));
		}


		private void RefreshLedData() {
			RefreshLedEvent();
		}

		// We call this one to send colors to everything, including the color service
		public async void SendColors(List<Color> c1, List<Color> c2, int fadeTime = 0) {
			await TriggerSendColorEvent.InvokeAsync(this, new DynamicEventArgs(c1, c2, fadeTime));
		}

		
		public void TriggerDreamSubscribe() {
			DreamSubscribeEvent();
		}


		public async Task NotifyClients() {
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

		
		public void RefreshDreamscreen(in CancellationToken csToken) {
			RefreshDreamscreenEvent(csToken);
		}

		public async Task AuthorizeNano(string id) {
			var leaf = DataUtil.GetCollectionItem<NanoleafData>("Dev_Nano", id);
			bool doAuth = leaf.Token == null;
			if (doAuth) {
				await _hubContext.Clients.All.SendAsync("nanoAuth", "authorized");
				await _hubContext.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
				return;
			}

			var panel = new NanoleafDevice(leaf, HttpSender);
			var count = 0;
			while (count < 30) {
				var appKey = panel.CheckAuth().Result;
				if (appKey != null) {
					leaf.Token = appKey.Token;
					DataUtil.InsertCollection<NanoleafData>("Dev_Nanoleaf", leaf);
					await _hubContext.Clients.All.SendAsync("nanoAuth", "authorized");
					await _hubContext.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
					panel.Dispose();
					return;
				}

				await _hubContext.Clients.All.SendAsync("nanoAuth", count);
				Thread.Sleep(1000);
				count++;
			}

			await _hubContext.Clients.All.SendAsync("nanoAuth", "stop");

			panel.Dispose();
		}

		public async Task UpdateLed(LedData ld) {
			Log.Debug("Got LD from post: " + JsonConvert.SerializeObject(ld));
			await DataUtil.InsertCollection<LedData>("LedData", ld);
			RefreshLedData();
			await NotifyClients();
		}

		public async Task UpdateSystem(SystemData sd) {
			SystemData oldSd = DataUtil.GetObject<SystemData>("SystemData");
			if (oldSd.LedCount != sd.LedCount) {
				var leds = DataUtil.GetCollection<LedData>("LedData");
				foreach (var led in leds.Where(led => led.Count == 0)) {
					led.Count = sd.LedCount;
					await DataUtil.InsertCollection<LedData>("LedData", led);
				}
			}
			RefreshSystemEvent();
			DataUtil.SetObject<SystemData>("SystemData", sd);
		}

		public static Task SystemControl(string action) {
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
			return Task.CompletedTask;
		}

		public async Task UpdateDevice(JObject device) {
			Log.Debug("Update device called!");
			var tag = (string) device.GetValue("Tag");
			var id = (string) device.GetValue("_id");
			device["Id"] = id;
			Log.Debug($"ID and tag are {id} and {tag}.");
			var updated = false;
			try {
				switch (tag) {
					case "Wled":
						await DataUtil.InsertCollection<WledData>("Dev_Wled", device.ToObject<WledData>());
						updated = true;
						break;
					case "Lifx":
						await DataUtil.InsertCollection<LifxData>("Dev_Lifx", device.ToObject<LifxData>());
						updated = true;
						break;
					case "HueBridge":
						var dev = device.ToObject<HueData>();
						await DataUtil.InsertCollection<HueData>("Dev_Hue", dev);
						updated = true;
						break;
					case "Nanoleaf":
						await DataUtil.InsertCollection<NanoleafData>("Dev_Nanoleaf", device.ToObject<NanoleafData>());
						updated = true;
						break;
					case "Dreamscreen":
						await DataUtil.InsertCollection<DreamData>("Dev_Dreamscreen", device.ToObject<DreamData>());
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
				await RefreshDevice(id);
			} else {
				Log.Debug("Sigh, no update...");
			}
		}

		public async Task FlashDevice(string deviceId) {
			await FlashDeviceEvent.InvokeAsync(this, new DynamicEventArgs(deviceId));
		}

		public async Task FlashSector(int sector) {
			await FlashSectorEvent.InvokeAsync(this, new DynamicEventArgs(sector));
		}

		public async Task DemoLed(string id) {
			await DemoLedEvent.InvokeAsync(this, new DynamicEventArgs(id));
		}
	}
}