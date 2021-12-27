#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using OpenRGB.NET;
using OpenRGB.NET.Models;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.OpenRgb {
	public class OpenRgbAgent : IColorTargetAgent {
		public bool Connected => _client?.Connected ?? false;
		private string Ip { get; set; } = "";
		private OpenRGBClient? _client;
		private Device[]? _devices;
		private int _port;

		public void Dispose() {
			_client?.Dispose();
			GC.SuppressFinalize(this);
		}

		public dynamic CreateAgent(ControlService cs) {
			_devices ??= GetDevices().ToArray();
			cs.RefreshSystemEvent += LoadClient;
			LoadClient();
			return this;
		}

		public IEnumerable<Device> GetDevices() {
			if (_client == null) {
				return new List<Device>();
			}

			Connect();
			if (!_client.Connected) {
				return new List<Device>();
			}

			_devices = _client.GetAllControllerData();
			return _devices;
		}

		public void Connect() {
			if (_client == null) {
				return;
			}

			if (_client.Connected) {
				return;
			}
			Log.Debug("Connecting OpenRGB client.");
			try {
				_client?.Connect();
			} catch (Exception) {
				Log.Warning("Could not connect to open RGB at " + Ip);
			}
		}

		public async Task<bool> SetMode(int deviceId, int mode) {
			if (_client == null) {
				return false;
			}

			if (!DeviceExists(deviceId)) {
				return false;
			}

			try {
				var cts = new CancellationTokenSource();
				cts.CancelAfter(500);
				Task.Run(() => {
					Connect();
					if (!_client.Connected) {
						return false;
					}
					_client.SetMode(deviceId, mode);
					return true;
				}, cts.Token).ConfigureAwait(false);
			} catch (Exception e) {
				Log.Warning("Exception starting OpenRGB: " + e.Message);
			}

			return false;
		}

		public void Update(int deviceId, Color[] colors) {
			if (_client == null) {
				return;
			}

			if (!DeviceExists(deviceId)) {
				return;
			}

			Connect();
			if (!_client.Connected) {
				return;
			}

			_client.UpdateLeds(deviceId, colors);
		}

		private bool DeviceExists(int deviceId) {
			_devices ??= Array.Empty<Device>();
			return deviceId >= _devices.Length - 1;
		}

		private void LoadClient() {
			var sd = DataUtil.GetSystemData();
			var ip = sd.OpenRgbIp;
			var port = sd.OpenRgbPort;
			if (ip != Ip || port != _port || _client == null) {
				Ip = ip;
				_port = port;
				Log.Debug("Creating new client.");
				_client = new OpenRGBClient(Ip, _port, "Glimmr", false);
				Log.Debug("Created.");
			}

			Connect();
		}
	}
}