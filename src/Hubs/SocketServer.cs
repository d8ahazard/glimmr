using System;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models;
using Glimmr.Models.ColorTarget.LED;
using Glimmr.Models.Util;
using Glimmr.Services;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json.Linq;
using Serilog;

namespace Glimmr.Hubs {
	public class SocketServer : Hub {
		private readonly ControlService _cs;
		

		public SocketServer(ControlService cs) {
			_cs = cs;
		}

		public void Mode(int mode) {
			Log.Debug("Mode set to: " + mode);
			_cs.SetMode(mode);
		}
		
		public Task ScanDevices() {
			Log.Debug("Scan called from socket!");
			_cs.ScanDevices();
			return Task.CompletedTask;
		}
		
		public void AuthorizeHue(string id) {
			_cs.AuthorizeHue(id);
		}
		
		public void AuthorizeNano(string id) {
			_cs.AuthorizeNano(id);
		}

		private CpuData GetStats(CancellationToken token) {
			return CpuUtil.GetStats();
		}

		public void LoadData() {
			Clients.Caller.SendAsync("olo", DataUtil.GetStoreSerialized());
		}

		
		public void SystemData(string sd) {
			Log.Debug("Updating system data.");
			var sdd = JObject.Parse(sd);
			var sd2 = sdd.ToObject<SystemData>();
			_cs.UpdateSystem(sd2);
		}
		
		public void LedData(string ld) {
			Log.Debug("Updating LED Data.");
			var ldd = JObject.Parse(ld).ToObject<LedData>();
			_cs.UpdateLed(ldd);
		}

		public void DemoLed(string id) {
			Log.Debug("We should demo a strip here.");
			_cs.DemoLed(id);
		}

		public void SystemControl(string action) {
			ControlService.SystemControl(action);
		}

		public void UpdateDevice(string deviceJson) {
			var device = JObject.Parse(deviceJson);
			_cs.UpdateDevice(device);
		}

		public void FlashDevice(string deviceId) {
			_cs.FlashDevice(deviceId);
		}

		public void FlashSector(int sector) {
			_cs.FlashSector(sector);
		}

		public void FlashLed(int led) {
			Log.Debug("Get got: " + led);
			_cs.TestLights(led);
		}

		
		public override Task OnDisconnectedAsync(Exception exception) {
			var dc = base.OnDisconnectedAsync(exception);
			return dc;
		}

		public override Task OnConnectedAsync() {
			Clients.Caller.SendAsync("olo", DataUtil.GetStoreSerialized());
			var bc = base.OnConnectedAsync();
			return bc;
		}
	}
}