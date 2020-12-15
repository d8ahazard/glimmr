using System;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.StreamingDevice.Dreamscreen;
using Glimmr.Models.StreamingDevice.Hue;
using Glimmr.Models.StreamingDevice.LIFX;
using Glimmr.Models.StreamingDevice.Nanoleaf;
using Glimmr.Models.StreamingDevice.WLED;
using Glimmr.Models.Util;
using Glimmr.Services;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace Glimmr.Hubs {
	public class SocketServer : Hub {
		private readonly ControlService _cs;
		

		public SocketServer(ControlService cs) {
			_cs = cs;
		}

		public Task Mode(int mode) {
			Log.Debug($"WS Mode: {mode}");
			_cs.SetMode(mode);
			return Clients.All.SendAsync("mode", mode);
		}

		public Task Action(string action, string value) {
			Log.Debug($"WS Action: {action}: " + JsonConvert.SerializeObject(value));
			return Clients.Caller.SendAsync("ack", true);
		}

		public Task ScanDevices() {
			Log.Debug("Scan called from socket!");
			_cs.ScanDevices();
			return Task.CompletedTask;
		}

		private CpuData GetStats(CancellationToken token) {
			return CpuUtil.GetStats();
		}

		public async void AuthorizeHue(string id) {
			Log.Debug("AuthHue called, for real (socket): " + id);
			HueData bd;
			if (!string.IsNullOrEmpty(id)) {
				await Clients.All.SendAsync("hueAuth", "start");
				bd = DataUtil.GetCollectionItem<HueData>("Dev_Hue", id);
				Log.Debug("BD: " + JsonConvert.SerializeObject(bd));
				if (bd == null) {
					Log.Debug("Null bridge retrieved.");
					await Clients.All.SendAsync("hueAuth", "stop");
					return;
				}

				if (bd.Key != null && bd.User != null) {
					Log.Debug("Bridge is already authorized.");
					await Clients.All.SendAsync("hueAuth", "authorized");
					await Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
					return;
				}
			} else {
				Log.Warning("Null value.");
				await Clients.All.SendAsync("hueAuth", "stop");
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
							await Clients.All.SendAsync("hueAuth", "authorized");
							await Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
							return;
						}

						Log.Debug("Appkey is null?");
					}

					Log.Debug("Waiting for app key.");
				} catch (NullReferenceException e) {
					Log.Error("Null exception.", e);
				}

				await Clients.All.SendAsync("hueAuth", count);
				Thread.Sleep(1000);
			}

			Log.Debug("We should be authorized, returning.");
		}

		public async void UpdateLed(string ld) {
			Log.Debug("This worked: " + JsonConvert.SerializeObject(ld));
			//DataUtil.SetObject("LedData", ld);
			//_cs.RefreshDevices();
		}

		public async void UpdateDevice(string deviceJson) {
			var device = JObject.Parse(deviceJson);
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
				_cs.RefreshDevice(id);
			} else {
				Log.Debug("Sigh, no update...");
			}
		}

		public async void AuthorizeNano(string id) {
			var leaf = DataUtil.GetCollectionItem<NanoleafData>("Dev_Nano", id);
			bool doAuth = leaf.Token == null;
			if (doAuth) {
				await Clients.All.SendAsync("nanoAuth", "authorized");
				await Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
				return;
			}

			var panel = new NanoleafDevice(id);
			var count = 0;
			while (count < 30) {
				var appKey = panel.CheckAuth().Result;
				if (appKey != null) {
					leaf.Token = appKey.Token;
					DataUtil.InsertCollection<NanoleafData>("Dev_Nanoleaf", leaf);
					await Clients.All.SendAsync("nanoAuth", "authorized");
					await Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
					panel.Dispose();
					return;
				}

				await Clients.All.SendAsync("nanoAuth", count);
				Thread.Sleep(1000);
				count++;
			}

			await Clients.All.SendAsync("nanoAuth", "stop");

			panel.Dispose();
		}

		public override Task OnDisconnectedAsync(Exception exception) {
			var dc = base.OnDisconnectedAsync(exception);
			return dc;
		}

		public override Task OnConnectedAsync() {
			var bc = base.OnConnectedAsync();
			return bc;
		}
	}
}