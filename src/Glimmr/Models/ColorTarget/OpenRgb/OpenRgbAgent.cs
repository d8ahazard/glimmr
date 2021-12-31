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

namespace Glimmr.Models.ColorTarget.OpenRgb; 

public abstract class OpenRgbAgent : IColorTargetAgent {
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
		try {
			_client?.Connect();
		} catch (Exception e) {
			Log.Warning($"Could not connect to open RGB {Ip}: " + e.Message);
		}
	}

	public bool SetMode(int deviceId, int mode) {
		if (_client == null) {
			return false;
		}

		if (!DeviceExists(deviceId)) {
			return false;
		}

		try {
			if (!_client.Connected) {
				return false;
			}
			_client.SetMode(deviceId, mode);
			return true;
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
			_client = new OpenRGBClient(Ip, _port, "Glimmr", false);
		}

		Connect();
	}
}