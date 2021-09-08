#region

using System;
using Glimmr.Models.Util;
using Glimmr.Services;
using OpenRGB.NET;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.OpenRgb {
	public class OpenRgbAgent : IColorTargetAgent {
		private string Ip { get; set; } = "";
		private OpenRGBClient? _client;
		private int _port;

		public void Dispose() {
			_client?.Dispose();
		}

		public dynamic? CreateAgent(ControlService cs) {
			cs.RefreshSystemEvent += LoadClient;
			LoadClient();
			return _client;
		}

		private void LoadClient() {
			var sd = DataUtil.GetSystemData();
			var ip = sd.OpenRgbIp;
			var port = sd.OpenRgbPort;
			if (ip == Ip && port == _port && _client != null) {
				Log.Debug("Nothing to do?");
				return;
			}

			Ip = ip;
			_port = port;
			_client?.Dispose();
			_client = null;
			Log.Debug("Creating new client.");
			_client = new OpenRGBClient(Ip, _port, "Glimmr",false);
			Log.Debug("Created.");
			try {
				_client.Connect();
				Log.Debug("Connected");
			} catch (Exception e) {
				Log.Warning("Exception creating client: " + e.Message);
			}
		}
	}
}