#region

using System;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Led;

public class LedDiscovery : ColorDiscovery, IColorDiscovery {
	/// <summary>
	///     Set GLIMMR_DEV_LEDS=1 to create the GPIO LED strip devices on non-Pi
	///     machines so the mapping UI can be exercised during development.
	///     The LED agent stays disabled, so no GPIO output is ever attempted.
	/// </summary>
	private static bool DevLedsEnabled => Environment.GetEnvironmentVariable("GLIMMR_DEV_LEDS") == "1";

	public LedDiscovery(ColorService colorService) : base(colorService) { }

	public async Task Discover(int timeout, CancellationToken ct) {
		DataUtil.DeleteDevice("2");

		if (!SystemUtil.IsRaspberryPi() && !DevLedsEnabled) {
			DataUtil.DeleteDevice("0");
			DataUtil.DeleteDevice("1");
			Log.Debug("No, really, this is not a pi, we shouldn't be creating GPIO stuff here.");
			return;
		}

		// Brightness is a 0-100 percentage.
		var ld0 = new LedData { Id = "0", Brightness = 100, GpioNumber = 18, Enable = true };
		var ld1 = new LedData { Id = "1", Brightness = 100, GpioNumber = 19 };

		await ControlService.AddDevice(ld0);
		await ControlService.AddDevice(ld1);
	}
}