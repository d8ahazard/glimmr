#region

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.OpenRgb {
	public class OpenRgbDiscovery : ColorDiscovery, IColorDiscovery {
		private readonly OpenRgbAgent? _client;
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
				if (!_client.Connected) {
					try {
						_client.Connect();
					} catch (Exception e) {
						Log.Debug("Error connecting to client, probably it doesn't exist: " + e.Message);
						return;
					}
				}

				if (_client.Connected) {
					Log.Debug("Client connected.");
					var devs = _client.GetDevices().ToArray();
					var existing = DataUtil.GetDevices();
					for (var i = 0; i < devs.Length; i++) {
						var dev = devs[i];
						var rd = new OpenRgbData(dev, i, sd.OpenRgbIp);
						foreach (var od in from IColorTargetData ex in existing
							where ex.Id.Contains("OpenRgb")
							select (OpenRgbData)ex
							into od
							where od.Matches(dev)
							select od) {
							od.UpdateFromDiscovered(rd);
							rd = od;
						}

						await _cs.UpdateDevice(rd).ConfigureAwait(false);
					}
				}
			} catch (Exception f) {
				Log.Warning("Exception during OpenRGB Discovery: " + f.Message + " at " + f.StackTrace);
			}
		}
	}
}