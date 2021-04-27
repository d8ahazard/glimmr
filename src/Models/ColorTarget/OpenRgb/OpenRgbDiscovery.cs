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

		public async Task Discover(CancellationToken ct) {
			if (!_client.Connected) _client.Connect();
			
			if (_client.Connected) {
				var devs = _client.GetAllControllerData();
				var idx = 0;
				foreach (var dev in devs) {
					var rd = new OpenRgbData(dev) {Id = "OpenRgb" + idx, DeviceId = idx};
					Log.Debug("OpenRGB device found: " + JsonConvert.SerializeObject(rd));
					await _cs.UpdateDevice(rd).ConfigureAwait(false);
					idx++;
				}
			}
		}

		private void LoadData() {
			var sd = (string) DataUtil.GetItem("OpenRgbIp");
		}
	}
}