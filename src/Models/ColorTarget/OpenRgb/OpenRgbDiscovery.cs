using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using OpenRGB.NET;
using Serilog;

namespace Glimmr.Models.ColorTarget.OpenRgb {
	public class OpenRgbDiscovery : ColorDiscovery, IColorDiscovery {

		private OpenRGBClient _client;
		public override string DeviceTag { get; set; } = "OpenRgb";
		private ControlService _cs;
		
		public OpenRgbDiscovery(ColorService colorService) : base(colorService) {
			_client = colorService.ControlService.GetAgent("OpenRgbAgent");
			_cs = colorService.ControlService;
			LoadData();
		}

		public async Task Discover(CancellationToken ct, int timeout) {
			try {
				if (_client == null) return;
				if (!_client.Connected) {
					try {
						_client.Connect();
					} catch (Exception e) {
						Log.Debug("Error connecting to client, probably it doesn't exist.");
						return;
					}
				}

				if (_client == null) return;
				if (_client.Connected) {
					var devs = _client.GetAllControllerData();
					var idx = 0;
					foreach (var dev in devs) {
						var rd = new OpenRgbData(dev) {Id = "OpenRgb" + idx, DeviceId = idx};
						await _cs.UpdateDevice(rd).ConfigureAwait(false);
						idx++;
					}
				}
			} catch (Exception f) {
				Log.Warning("Exception: " + f.Message + " at " + f.StackTrace);
			}
		}

		private void LoadData() {
			var sd = (string) DataUtil.GetItem("OpenRgbIp");
		}
	}
}