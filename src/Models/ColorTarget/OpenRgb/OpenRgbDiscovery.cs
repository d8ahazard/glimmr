#region

using System;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using OpenRGB.NET;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.OpenRgb {
	public class OpenRgbDiscovery : ColorDiscovery, IColorDiscovery {
		public override string DeviceTag { get; set; } = "OpenRgb";

		private readonly OpenRGBClient? _client;
		private readonly ControlService _cs;

		public OpenRgbDiscovery(ColorService colorService) : base(colorService) {
			_client = colorService.ControlService.GetAgent("OpenRgbAgent");
			_cs = colorService.ControlService;
		}

		public async Task Discover(CancellationToken ct, int timeout) {
			if (_client == null) {
				Log.Debug("No client.");
				return;
			}
			try {
				var sd = DataUtil.GetSystemData();
				var ip = sd.OpenRgbIp;
				Log.Debug("Disco started...");

				if (!_client.Connected) {
					try {
						Log.Debug("Trying connection...");
						_client.Connect();
					} catch (Exception e) {
						Log.Debug("Error connecting to client, probably it doesn't exist: " + e.Message);
						return;
					}
				}

				if (_client.Connected) {
					Log.Debug("Client connected.");
					var devs = _client.GetAllControllerData();
					var idx = 0;
					foreach (var dev in devs) {
						var rd = new OpenRgbData(dev, idx, ip);
						await _cs.UpdateDevice(rd).ConfigureAwait(false);
						idx++;
					}
				}
			} catch (Exception f) {
				Log.Warning("Exception during OpenRGB Discovery: " + f.Message + " at " + f.StackTrace);
			}
		}

		
	}
}