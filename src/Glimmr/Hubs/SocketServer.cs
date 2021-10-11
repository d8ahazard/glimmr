#region

using System;
using System.Collections.Generic;
using System.Linq;
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
		private static Dictionary<string, bool> _states = new();
		private readonly ControlService _cs;
		private bool _doSend;


		public SocketServer(ControlService cs) {
			_cs = cs;
			_states = new Dictionary<string, bool>();
		}

		

		public async Task Mode(int mode) {
			Log.Debug("Mode set to: " + mode);
			try {
				await _cs.SetMode(mode);
			} catch (Exception e) {
				Log.Warning("Exception caught on mode change: " + e.Message + " at " + e.StackTrace);
			}
		}


		public async Task ScanDevices() {
			Log.Debug("Scan called from socket!");
			await _cs.ScanDevices();
		}

		public async Task AuthorizeDevice(string id) {
			var sender = Clients.Caller;
			await _cs.AuthorizeDevice(id, sender);
		}

		public async Task Store() {
			await Clients.Caller.SendAsync("olo", DataUtil.GetStoreSerialized(_cs));
		}

		public async Task DeleteDevice(string id) {
			Log.Debug("Deleting device: " + id);
			await _cs.RemoveDevice(id).ConfigureAwait(false);
		}

		public async Task SystemData(string sd) {
			var sdd = JObject.Parse(sd);
			var sd2 = sdd.ToObject<SystemData>();
			try {
				await _cs.UpdateSystem(sd2).ConfigureAwait(false);
				//await Clients.Others.SendAsync("olo", DataUtil.GetStoreSerialized());
			} catch (Exception e) {
				Log.Warning("Exception updating SD: " + e.Message + " at " + e.StackTrace);
			}
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
			Log.Debug("Got a device: " + JsonConvert.SerializeObject(device));
			var cTag = device.GetValue("tag");
			if (cTag == null) {
				Log.Debug("Unable to get tag.");
				return;
			}

			var tag = cTag.ToString();
			var className = "Glimmr.Models.ColorTarget." + tag + "." + tag + "Data";
			Log.Debug("Finding: " + className);
			var typeName = Type.GetType(className);
			if (typeName != null) {
				dynamic? devObject = device.ToObject(typeName);
				if (devObject != null) {
					await _cs.UpdateDevice(devObject, false).ConfigureAwait(false);
				}
			}
		}

		public async Task FlashDevice(string deviceId) {
			await _cs.FlashDevice(deviceId);
		}

		public Task SettingsShown(bool open) {
			Log.Debug("SS: " + open);
			var client = Context.ConnectionId;
			if (client == null) {
				Log.Warning("Unable to get client identity...");
				return Task.CompletedTask;
			}

			_states[client] = open;
			if (open) {
				Log.Debug("Open client: " + client);
			} else {
				Log.Debug("Close client: " + client);
			}

			SetSend();
			return Task.CompletedTask;
		}

		private void SetSend() {
			var send = _states.Any(state => state.Value);
			_doSend = send;
			_cs.SendPreview = _doSend;
		}

		public async Task FlashSector(int sector) {
			Log.Debug("Sector flash called for: " + sector);
			try {
				await _cs.FlashSector(sector);
			} catch (Exception e) {
				Log.Warning("Exception: " + e.Message + " at " + e.StackTrace);
			}
		}

		public async Task FlashLed(int led) {
			Log.Debug("Get got: " + led);
			await _cs.TestLights(led);
		}


		public override async Task OnConnectedAsync() {
			try {
				_states[Context.ConnectionId] = false;
				SetSend();
				Log.Debug("Connected: " + Context.ConnectionId);
				await Clients.Caller.SendAsync("olo", DataUtil.GetStoreSerialized(_cs));
			} catch (Exception) {
				// Ignored
			}

			await base.OnConnectedAsync();
		}

		public override Task OnDisconnectedAsync(Exception? exception) {
			_states.Remove(Context.ConnectionId);
			SetSend();
			return base.OnDisconnectedAsync(exception);
		}
	}
}