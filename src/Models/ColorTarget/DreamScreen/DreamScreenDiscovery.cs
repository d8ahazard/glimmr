using System.Threading;
using System.Threading.Tasks;
using DreamScreenNet;
using Glimmr.Services;

namespace Glimmr.Models.ColorTarget.DreamScreen {
	public class DreamScreenDiscovery : ColorDiscovery, IColorDiscovery {
		private DreamScreenClient _client;
		private ControlService _cs;
		public DreamScreenDiscovery(ColorService colorService) : base(colorService) {
			_client = colorService.ControlService.GetAgent("DreamAgent");
			_cs = colorService.ControlService;
		}

		public override string DeviceTag { get; set; }
		public async Task Discover(CancellationToken ct) {
			_client.DeviceDiscovered += DevFound;
			_client.StartDeviceDiscovery();
			while (!ct.IsCancellationRequested) {
				await Task.Delay(1, ct);
			}
			_client.StopDeviceDiscovery();
		}

		private void DevFound(object? sender, DreamScreenClient.DeviceDiscoveryEventArgs e) {
			var dd = new DreamScreenData(e.Device);
			_cs.AddDevice(dd).ConfigureAwait(false);
		}
	}
}