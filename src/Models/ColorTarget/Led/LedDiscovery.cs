using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;

namespace Glimmr.Models.ColorTarget.Led {
	public class LedDiscovery : ColorDiscovery, IColorDiscovery {
		public override string DeviceTag { get; set; }

		public LedDiscovery(ColorService colorService) : base(colorService) {
		}

		public async Task Discover(CancellationToken ct, int timeout) {
			DataUtil.DeleteDevice("2");

			if (!SystemUtil.IsRaspberryPi()) {
				DataUtil.DeleteDevice("0");
				DataUtil.DeleteDevice("1");
				Log.Debug("No, really, this is not a pi, we shouldn't be creating GPIO stuff here.");
				return;
			}

			var ld0 = new LedData {Id = "0", Brightness = 255, GpioNumber = 18, Enable = true};
			var ld1 = new LedData {Id = "1", Brightness = 255, GpioNumber = 19};

			await ControlService.AddDevice(ld0);
			await ControlService.AddDevice(ld1);
		}
	}
}