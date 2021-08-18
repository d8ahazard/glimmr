#region

using System;
using Glimmr.Models.Util;
using Glimmr.Services;
using OpenRGB.NET;

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
				return;
			}

			Ip = ip;
			_port = port;
			_client?.Dispose();
			try {
				_client = new OpenRGBClient(Ip, _port, "Glimmr");
				_client.Connect();
			} catch (Exception) {
				// ignored
			}
		}
	}
}