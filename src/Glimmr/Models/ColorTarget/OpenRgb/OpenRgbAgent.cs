#region

using System;
using System.Collections.Generic;
using System.Linq;
using Glimmr.Models.Util;
using Glimmr.Services;
using OpenRGB.NET;
using OpenRGB.NET.Models;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.OpenRgb {
	public class OpenRgbAgent : IColorTargetAgent {
		private string Ip { get; set; } = "";
		private OpenRGBClient? _client;
		private int _port;
		private Device[]? _devices;
		public bool Connected => _client?.Connected ?? false;
		
		public IEnumerable<Device> GetDevices() {
			if (_client == null) return new List<Device>();
			Connect();
			if (!_client.Connected) return new List<Device>();
			_devices = _client.GetAllControllerData();
			return _devices;
		}

		public void Connect() {
			if (_client == null) return;
			
			if (_client.Connected) {
				return;
			}
			Log.Debug("Connecting OpenRGB client.");
			try {
				_client?.Connect();
			} catch (ObjectDisposedException) {
				
			}
		}

		public void SetMode(int deviceId, int mode, Color[] colors) {
			if (_client == null) return;
			if (!DeviceExists(deviceId)) return;
			Connect();
			if (!_client.Connected) return;
			_client.SetMode(deviceId, mode);
		}

		public void Update(int deviceId, Color[] colors) {
			if (_client == null) return;
			if (!DeviceExists(deviceId)) return;
			Connect();
			if (!_client.Connected) return;
			_client.UpdateLeds(deviceId, colors);
		}

		private bool DeviceExists(int deviceId) {
			if (_devices == null) _devices = new Device[0];
			return deviceId >= _devices.Length - 1;
		}
		
		public void Dispose() {
			_client?.Dispose();
		}

		public dynamic CreateAgent(ControlService cs) {
			if (_devices == null) _devices = GetDevices().ToArray();
			cs.RefreshSystemEvent += LoadClient;
			LoadClient();
			return this;
		}

		private void LoadClient() {
			var sd = DataUtil.GetSystemData();
			var ip = sd.OpenRgbIp;
			var port = sd.OpenRgbPort;
			if (ip != Ip || port != _port || _client == null) {
				Ip = ip;
				_port = port;
				Log.Debug("Creating new client.");
				_client = new OpenRGBClient(Ip, _port, "Glimmr",false);
				Log.Debug("Created.");	
			}

			Connect();
		}
	}
}