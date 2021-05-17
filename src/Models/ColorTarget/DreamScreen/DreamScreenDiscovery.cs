using System;
using System.Threading;
using System.Threading.Tasks;
using DreamScreenNet;
using Glimmr.Services;
using Newtonsoft.Json;
using Org.BouncyCastle.Crypto.Digests;
using Serilog;

namespace Glimmr.Models.ColorTarget.DreamScreen {
	public class DreamScreenDiscovery : ColorDiscovery, IColorDiscovery {
		private DreamScreenClient _client;
		private ControlService _cs;
		public DreamScreenDiscovery(ColorService colorService) : base(colorService) {
			_client = colorService.ControlService.GetAgent("DreamAgent");
			_cs = colorService.ControlService;
		}

		public override string DeviceTag { get; set; }
		public async Task Discover(CancellationToken ct, int timeout) {
			Log.Debug("DS: Starting discovery...");
			_client.DeviceDiscovered += DevFound;
			_client.StartDeviceDiscovery();
			await Task.Delay(TimeSpan.FromSeconds(timeout));
			_client.StopDeviceDiscovery();
			_client.DeviceDiscovered -= DevFound;
			Log.Debug("DS: Discovery complete.");
		}

		private void DevFound(object? sender, DreamScreenClient.DeviceDiscoveryEventArgs e) {
			var dd = new DreamScreenData(e.Device);
			Log.Debug("Got one: " + JsonConvert.SerializeObject(dd));
			_cs.AddDevice(dd).ConfigureAwait(false);
		}
	}
}