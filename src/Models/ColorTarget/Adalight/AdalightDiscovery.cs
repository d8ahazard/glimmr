using System;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Services;
using Serilog;

namespace Glimmr.Models.ColorTarget.Adalight {
	public class AdalightDiscovery : ColorDiscovery, IColorDiscovery {
		private readonly ControlService _controlService;
		
		public AdalightDiscovery(ColorService cs) : base(cs) {
			_controlService = cs.ControlService;
		}
		public async Task Discover(CancellationToken ct, int timeout) {
			Log.Debug("Adalight: Discovery started.");
			var discoTask = Task.Run(() => {
				try {
					var devs = AdalightNet.Adalight.FindDevices();
					foreach (var dev in devs) {
						var count = dev.Value.Key;
						var bri = dev.Value.Value;
						var data = new AdalightData(dev.Key, count);
						if (bri != 0) data.Brightness = bri;
						_controlService.AddDevice(data).ConfigureAwait(false);
					}
				} catch (Exception e) {
					Log.Debug("Exception: " + e.Message);
				}
			}, ct);
			await discoTask;
			Log.Debug("Adalight: Discovery complete.");
		}

		public override string DeviceTag { get; set; } = "Adalight";
	}
}