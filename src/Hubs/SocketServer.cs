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

		public async Task Mode(int mode) {
			Log.Debug("Mode set to: " + mode);
			await _cs.SetMode(mode);
		}
		
		public Task ScanDevices() {
			Log.Debug("Scan called from socket!");
			_cs.ScanDevices();
			return Task.CompletedTask;
		}
		
		public async Task AuthorizeHue(string id) {
			await _cs.AuthorizeHue(id);
		}
		
		public async Task AuthorizeNano(string id) {
			await _cs.AuthorizeNano(id);
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

		public async Task DemoLed(string id) {
			Log.Debug("We should demo a strip here.");
			await _cs.DemoLed(id);
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

		public Task FlashSector(int sector) {
			_cs.FlashSector(sector);
			return Task.CompletedTask;
		}

		public Task FlashLed(int led) {
			Log.Debug("Get got: " + led);
			_cs.TestLights(led);
			return Task.CompletedTask;
		}


		public override Task OnConnectedAsync() {
			Clients.Caller.SendAsync("olo", DataUtil.GetStoreSerialized());
			return base.OnConnectedAsync();
		}
	}
}