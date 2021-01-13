﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models;
using Glimmr.Models.LED;
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

		public void Mode(int mode) {
			Log.Debug("Mode set to: " + mode);
			_cs.SetMode(mode);
		}
		
		public Task ScanDevices() {
			Log.Debug("Scan called from socket!");
			_cs.ScanDevices();
			return Task.CompletedTask;
		}

		public void AmbientShow(int show, string deviceId) {
			_cs.SetAmbientShow(show, deviceId);
		}
		
		public async void AuthorizeHue(string id) {
			_cs.AuthorizeHue(id);
		}
		
		public async void AuthorizeNano(string id) {
			_cs.AuthorizeNano(id);
		}

		private CpuData GetStats(CancellationToken token) {
			return CpuUtil.GetStats();
		}

		public void LoadData() {
			Clients.Caller.SendAsync("olo", DataUtil.GetStoreSerialized());
		}

		
		public async void SystemData(string sd) {
			var sdd = JObject.Parse(sd);
			var sd2 = sdd.ToObject<SystemData>();
			_cs.UpdateSystem(sd2);
		}
		
		public async void LedData(string ld) {
			Log.Debug("Updating LED Data.");
			var ldd = JObject.Parse(ld).ToObject<LedData>();
			_cs.UpdateLed(ldd);
		}

		public void SystemControl(string action) {
			_cs.SystemControl(action);
		}

		public async void UpdateDevice(string deviceJson) {
			var device = JObject.Parse(deviceJson);
			_cs.UpdateDevice(device);
		}

		public async void FlashDevice(string deviceId) {
			_cs.FlashDevice(deviceId);
		}

		public void FlashSector(int sector) {
			_cs.FlashSector(sector);
		}

		public void FlashLed(int led) {
			Log.Debug("Get got: " + led);
			_cs.TestLeds(led);
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