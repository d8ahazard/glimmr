#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Models;
using Glimmr.Models.ColorTarget;
using Glimmr.Models.Util;
using Glimmr.Services;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Serilog;

#endregion

namespace Glimmr.Hubs;

public class SocketServer : Hub {
	private static Dictionary<string, bool> _states = new();
	private readonly ControlService? _cs;
	private bool _doSend;


	public SocketServer() {
		var cs = ControlService.GetInstance();
		if (cs != null) {
			_cs = cs;
		}

		_states = new Dictionary<string, bool>();
	}


	public async Task Mode(DeviceMode mode) {
		if (_cs == null) {
			Log.Debug("NO CONTROL SERVICE!");
			return;
		}
		Log.Debug("HUB MODE: " + mode);
		try {
			await _cs.SetMode(mode).ConfigureAwait(false);
		} catch (Exception e) {
			Log.Warning("Exception caught on mode change: " + e.Message + " at " + e.StackTrace);
		}
	}


	public async Task ScanDevices() {
		if (_cs == null) {
			Log.Debug("NO CONTROL SERVICE!");
			return;
		}

		await _cs.ScanDevices();
	}

	public async Task AuthorizeDevice(string id) {
		if (_cs == null) {
			Log.Debug("NO CONTROL SERVICE!");
			return;
		}

		var sender = Clients.Caller;
		await _cs.AuthorizeDevice(id, sender);
	}

	public async Task Store() {
		if (_cs == null) {
			Log.Debug("NO CONTROL SERVICE!");
			return;
		}

		await Clients.Caller.SendAsync("olo", DataUtil.GetStoreSerialized(_cs));
	}

	public async Task DeleteDevice(string id) {
		if (_cs == null) {
			Log.Debug("NO CONTROL SERVICE!");
			return;
		}

		await _cs.RemoveDevice(id).ConfigureAwait(false);
	}

	public async Task SystemData(string sd) {
		if (_cs == null) {
			Log.Debug("NO CONTROL SERVICE!");
			return;
		}

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
		if (_cs == null) {
			Log.Debug("NO CONTROL SERVICE!");
			return;
		}

		await _cs.DemoLed(id).ConfigureAwait(false);
	}

	public async Task SystemControl(string action) {
		await ControlService.SystemControl(action).ConfigureAwait(false);
	}

	public async Task UpdateDevice(string deviceJson) {
		if (_cs == null) {
			Log.Debug("NO CONTROL SERVICE!");
			return;
		}

		var device = JObject.Parse(deviceJson);
		var cTag = device.GetValue("tag");
		var cId = device.GetValue("id");
		var id = string.Empty;
		if (cTag == null) {
			Log.Warning("Unable to get tag.");
			return;
		}

		if (cId != null) {
			id = cId.ToString();
		}

		var tag = cTag.ToString();
		var className = "Glimmr.Models.ColorTarget." + tag + "." + tag + "Data";
		var typeName = Type.GetType(className);
		if (typeName != null) {
			dynamic? devObject = device.ToObject(typeName);
			if (devObject != null) {
				await _cs.UpdateDevice(devObject, false).ConfigureAwait(false);
				if (string.IsNullOrEmpty(id)) return;
				var data = DataUtil.GetDevice(id);
				if (data == null) return;
				var serializerSettings = new JsonSerializerSettings {
					ContractResolver = new CamelCasePropertyNamesContractResolver()
				};

				await Clients.All.SendAsync("device", JsonConvert.SerializeObject((IColorTargetData)data, serializerSettings));
			}
		}
	}

	public async Task FlashDevice(string deviceId) {
		if (_cs == null) {
			Log.Debug("NO CONTROL SERVICE!");
			return;
		}

		await _cs.FlashDevice(deviceId);
	}

	public Task SettingsShown(bool open) {
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
		if (_cs == null) {
			Log.Debug("NO CONTROL SERVICE!");
			return;
		}

		var send = _states.Any(state => state.Value);
		_doSend = send;
		_cs.SendPreview = _doSend;
	}

	public async Task FlashSector(int sector) {
		if (_cs == null) {
			Log.Debug("NO CONTROL SERVICE!");
			return;
		}

		Log.Debug("Sector flash called for: " + sector);
		try {
			await _cs.FlashSector(sector);
		} catch (Exception e) {
			Log.Warning("Exception: " + e.Message + " at " + e.StackTrace);
		}
	}

	public async Task FlashLed(int led) {
		if (_cs == null) {
			Log.Debug("NO CONTROL SERVICE!");
			return;
		}

		Log.Debug("Get got: " + led);
		await _cs.TestLights(led);
	}


	public override async Task OnConnectedAsync() {
		if (_cs == null) {
			Log.Debug("NO CONTROL SERVICE!");
			return;
		}

		try {_states[Context.ConnectionId] = false;
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