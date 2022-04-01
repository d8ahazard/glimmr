#region

using System;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;
using YeelightAPI;

#endregion

namespace Glimmr.Models.ColorTarget.Yeelight;

public class YeelightDiscovery : ColorDiscovery, IColorDiscovery {
	private readonly Progress<Device> _reporter;

	public YeelightDiscovery(ColorService colorService) : base(colorService) {
		_reporter = new Progress<Device>(DeviceFound);
	}

	public async Task Discover(int timeout, CancellationToken ct) {
		Log.Debug("Yeelight: Discovery started...");
		// Await the asynchronous call to the static API
		try {
			await DeviceLocator.DiscoverAsync(_reporter, ct);
		} catch (Exception) {
			// Ignore error thrown on cancellation.
		}

		Log.Debug("Yeelight: Discovery complete.");
	}

	private static void DeviceFound(Device dev) {
		var ip = IpUtil.GetIpFromHost(dev.Hostname);
		var ipString = ip == null ? "" : ip.ToString();
		var yd = new YeelightData {
			Id = dev.Id, IpAddress = ipString, Name = dev.Name
		};
		ControlService.AddDevice(yd).ConfigureAwait(false);
	}
}