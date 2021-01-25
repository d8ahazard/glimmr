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
		
		public async Task ScanDevices() {
			Log.Debug("Scan called from socket!");
			await _cs.ScanDevices();
		}
		
		public async Task AuthorizeHue(string id) {
			await _cs.AuthorizeHue(id);
		}
		
		public async Task AuthorizeNano(string id) {
			await _cs.AuthorizeNano(id);
		}

		private async Task<CpuData> GetStats(CancellationToken token) {
			return await CpuUtil.GetStats();
		}

		public async Task LoadData() {
			await Clients.Caller.SendAsync("olo", DataUtil.GetStoreSerialized());
		}

		
		public async Task SystemData(string sd) {
			Log.Debug("Updating system data.");
			var sdd = JObject.Parse(sd);
			var sd2 = sdd.ToObject<SystemData>();
			await _cs.UpdateSystem(sd2);
		}
		
		public async Task LedData(string ld) {
			Log.Debug("Updating LED Data.");
			var ldd = JObject.Parse(ld).ToObject<LedData>();
			await _cs.UpdateLed(ldd);
		}

		public async Task DemoLed(string id) {
			Log.Debug("We should demo a strip here.");
			await _cs.DemoLed(id);
		}

		public async Task SystemControl(string action) {
			await ControlService.SystemControl(action);
		}

		public async Task UpdateDevice(string deviceJson) {
			var device = JObject.Parse(deviceJson);
			await _cs.UpdateDevice(device);
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
			Clients.Caller.SendAsync("olo", DataUtil.GetStoreSerialized());
			return base.OnConnectedAsync();
		}
	}
}