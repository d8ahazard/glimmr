using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Services;

namespace Glimmr.Models.ColorTarget.Led {
	public class LedDiscovery : ColorDiscovery, IColorDiscovery {
		public override string DeviceTag { get; set; }

		public LedDiscovery(ColorService colorService) : base(colorService) {
		}

		public async Task Discover(CancellationToken ct, int timeout) {
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
				return;
			}

			var ld0 = new LedData {Id = "0", Brightness = 255, GpioNumber = 18};
			var ld1 = new LedData {Id = "1", Brightness = 255, GpioNumber = 19};
			var ld2 = new LedData {Id = "2", Brightness = 255, GpioNumber = 10};

			await ControlService.AddDevice(ld0);
			await ControlService.AddDevice(ld1);
			await ControlService.AddDevice(ld2);
		}
	}
}