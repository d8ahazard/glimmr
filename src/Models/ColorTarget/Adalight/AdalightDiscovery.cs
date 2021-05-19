using System.Threading;
using System.Threading.Tasks;
using Glimmr.Services;

namespace Glimmr.Models.ColorTarget.Adalight {
	public class AdalightDiscovery : ColorDiscovery, IColorDiscovery {
		private readonly ControlService _controlService;
		
		public AdalightDiscovery(ColorService cs) : base(cs) {
			_controlService = cs.ControlService;
		}
		public async Task Discover(CancellationToken ct, int timeout) {
			var devs = AdalightNet.Adalight.FindDevices();
			foreach (var dev in devs) {
				await _controlService.AddDevice(new AdalightData(dev));
			}
		}

		public override string DeviceTag { get; set; } = "Adalight";
	}
}