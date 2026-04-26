#region

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Services;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.SignalRgb;

public class SignalRgbDiscovery : ColorDiscovery, IColorDiscovery {
	private readonly ControlService _cs;

	public SignalRgbDiscovery(ColorService colorService) : base(colorService) {
		_cs = colorService.ControlService;
	}

	public async Task Discover(int timeout, CancellationToken ct) {
		Log.Debug("SignalRGB: Discovery started...");
		try {
			using var client = new HttpClient();
			client.Timeout = TimeSpan.FromSeconds(5);
			var response = await client.GetAsync("http://127.0.0.1:16038/api/v1/lighting", ct);
			if (response.IsSuccessStatusCode) {
				var data = new SignalRgbData("SignalRgb_Local", "SignalRGB");
				await ControlService.AddDevice(data);
				Log.Debug("SignalRGB: Device found and added.");
			}
		} catch (Exception e) {
			Log.Debug("SignalRGB: Not reachable - " + e.Message);
		}

		Log.Debug("SignalRGB: Discovery complete.");
	}
}
