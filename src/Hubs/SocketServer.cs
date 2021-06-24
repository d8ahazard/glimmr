#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Glimmr.Models;
using Glimmr.Models.Util;
using Glimmr.Services;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

#endregion

namespace Glimmr.Hubs {
	public class SocketServer : Hub {
		private readonly ControlService _cs;


		public SocketServer(ControlService cs) {
			_cs = cs;
		}

		public async Task Mode(int mode) {
			Log.Debug("Mode set to: " + mode);
			await _cs.SetMode(mode);
		}

		public async Task ScanDevices() {
			Log.Debug("Scan called from socket!");
			await _cs.ScanDevices();
		}

		public async Task AuthorizeDevice(string id) {
			var sender = Clients.Caller;
			await _cs.AuthorizeDevice(id, sender);
		}

		public async Task LoadData() {
			await Clients.Caller.SendAsync("olo", DataUtil.GetStoreSerialized());
		}

		public async Task DeleteDevice(string id) {
			Log.Debug("Deleting device: " + id);
			await _cs.RemoveDevice(id).ConfigureAwait(false);
		}

		public async Task SystemData(string sd) {
			var sdd = JObject.Parse(sd);
			var sd2 = sdd.ToObject<SystemData>();
			Log.Debug("Updating system data: " + JsonConvert.SerializeObject(sd2));
			await _cs.UpdateSystem(sd2).ConfigureAwait(false);
		}

		public async Task DemoLed(string id) {
			Log.Debug("We should demo a strip here.");
			await _cs.DemoLed(id).ConfigureAwait(false);
		}

		public async Task SystemControl(string action) {
			await ControlService.SystemControl(action).ConfigureAwait(false);
		}

		public async Task UpdateDevice(string deviceJson) {
			var device = JObject.Parse(deviceJson);
			var cTag = device.GetValue("Tag");
			if (cTag == null) return;  
			var tag = cTag.ToString();
			var className = "Glimmr.Models.ColorTarget." + tag + "." + tag + "Data";
			var typeName = Type.GetType(className);
			if (typeName != null) {
				dynamic? devObject = device.ToObject(typeName);
				if (devObject != null) {
					await _cs.UpdateDevice(devObject, false).ConfigureAwait(false);
				}	
			}
		}

		public async Task Monitors(string deviceArray) {
			Log.Debug("Mon string: " + deviceArray);
			var monitors = JsonConvert.DeserializeObject<List<MonitorInfo>>(deviceArray);
			if (monitors == null) return;
			foreach (var mon in monitors) {
				Log.Debug("Inserting monitor: " + JsonConvert.SerializeObject(mon));
				await DataUtil.InsertCollection<MonitorInfo>("Dev_Video", mon);
			}

			await _cs.UpdateSystem();
		}

		public async Task FlashDevice(string deviceId) {
			await _cs.FlashDevice(deviceId);
		}

		public async Task FlashSector(int sector) {
			await _cs.FlashSector(sector);
		}

		public async Task FlashLed(int led) {
			Log.Debug("Get got: " + led);
			await _cs.TestLights(led);
		}


		public override Task OnConnectedAsync() {
			try {
				Clients.Caller.SendAsync("olo", DataUtil.GetStoreSerialized());
			} catch (Exception) {
				// Ignored
			}

			return base.OnConnectedAsync();
		}
	}
}