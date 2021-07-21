#region

using System;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Services;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Adalight {
	public class AdalightDiscovery : ColorDiscovery, IColorDiscovery {
		public override string DeviceTag { get; set; } = "Adalight";
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
						try {
							Log.Debug("Trying: " + dev.Key);
							var ac = new AdalightNet.Adalight(dev.Key, 20);
							ac.Connect();
							if (ac.Connected) {
								Log.Debug("Connected.");
								var foo = ac.GetState();
								count = foo[0];
								bri = foo[0];
								ac.Disconnect();
								ac.Dispose();
							} else {
								Log.Debug("Not connected...");
							}
						} catch (Exception e) {
							Log.Debug("Discovery exception: " + e.Message + " at " + e.StackTrace);
						}


						var data = new AdalightData(dev.Key, count);
						if (bri != 0) {
							data.Brightness = bri;
						}

						ControlService.AddDevice(data).ConfigureAwait(false);
					}
				} catch (Exception e) {
					Log.Debug("Exception: " + e.Message);
				}
			}, ct);
			await discoTask;
			Log.Debug("Adalight: Discovery complete.");
		}
	}
}