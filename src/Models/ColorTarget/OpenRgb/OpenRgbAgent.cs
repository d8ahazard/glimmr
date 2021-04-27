using System;
using Glimmr.Models.Util;
using Glimmr.Services;
using OpenRGB.NET;
using Serilog;

namespace Glimmr.Models.ColorTarget.OpenRgb {
	public class OpenRgbAgent : IColorTargetAgent {
		private OpenRGBClient _client;
		private string _ip;
		private int _port;
			
		public void Dispose() {
			
		}

		public OpenRgbAgent() {
			
		}

		public dynamic CreateAgent(ControlService cs) {
			var ip = (string) DataUtil.GetItem<string>("OpenRgbIp");
			var port = (int) DataUtil.GetItem<int>("OpenRgbPort");
			if (ip == _ip && port == _port && _client != null) {
				return _client;
			}

			_ip = ip;
			_port = port;

			_client?.Dispose();
			try {
				Log.Debug($"Trying to create agent at {_ip}");
				_client = new OpenRGBClient(_ip, _port, "Glimmr");
				_client.Connect();
				if (_client.Connected) {
					Console.WriteLine("RGB Client Connected!");
				}
			} catch (Exception) {
				// ignored
			}

			
			return _client;
		}
	}
}