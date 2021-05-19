using System;
using Glimmr.Models.Util;
using Glimmr.Services;
using OpenRGB.NET;

namespace Glimmr.Models.ColorTarget.OpenRgb {
	public class OpenRgbAgent : IColorTargetAgent {
		public string Ip { get; set; }
		private OpenRGBClient _client;
		private int _port;

		public void Dispose() {
		}

		public dynamic CreateAgent(ControlService cs) {
			var ip = (string) DataUtil.GetItem<string>("OpenRgbIp");
			var port = (int) DataUtil.GetItem<int>("OpenRgbPort");
			if (ip == Ip && port == _port && _client != null) {
				return _client;
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

			return _client;
		}
	}
}