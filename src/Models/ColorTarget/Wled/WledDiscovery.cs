using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Services;
using Makaretu.Dns;
using Serilog;

namespace Glimmr.Models.ColorTarget.Wled {
	public class WledDiscovery : ColorDiscovery, IColorDiscovery {
		public override string DeviceTag { get; set; }
		private readonly ControlService _controlService;

		private readonly MulticastService _mDns;
		private readonly ServiceDiscovery _sd;
		private bool _discovering;
		private bool _stopDiscovery;

		public WledDiscovery(ColorService cs) : base(cs) {
			_mDns = cs.ControlService.MulticastService;
			_controlService = cs.ControlService;
			_sd = _controlService.ServiceDiscovery;
			DeviceTag = "Wled";
		}

		public async Task Discover(CancellationToken ct, int timeout) {
			Log.Debug("WLED: Discovery started...");

			try {
				_mDns.Start();
				_mDns.NetworkInterfaceDiscovered += InterfaceDiscovered;
				_sd.ServiceDiscovered += ServiceDiscovered;
				_sd.ServiceInstanceDiscovered += WledDiscovered;
				_sd.QueryServiceInstances("_wled._tcp");
				_sd.QueryServiceInstances("_arduino._tcp");
				_mDns.SendQuery("_arduino._tcp", type: DnsType.PTR);
				_mDns.SendQuery("_wled._tcp", type: DnsType.PTR);
				await Task.Delay(TimeSpan.FromSeconds(timeout), CancellationToken.None);
				_mDns.NetworkInterfaceDiscovered -= InterfaceDiscovered;
				_sd.ServiceDiscovered -= ServiceDiscovered;
				_sd.ServiceInstanceDiscovered -= WledDiscovered;
				//_mDns.Stop();
				_stopDiscovery = true;
			} catch {
				// Ignore collection modified exception
			}

			Log.Debug("WLED: Discovery complete.");
		}

		private void ServiceDiscovered(object? sender, DomainName serviceName) {
			Log.Debug("Querying: " + serviceName);
			if (!_stopDiscovery) {
				_mDns.SendQuery(serviceName, type: DnsType.PTR);
			}
		}

		private void InterfaceDiscovered(object? sender, NetworkInterfaceEventArgs e) {
			_sd.QueryServiceInstances("_wled._tcp");
			_sd.QueryServiceInstances("_arduino._tcp");
		}

		private void WledDiscovered(object sender, ServiceInstanceDiscoveryEventArgs e) {
			var name = e.ServiceInstanceName.ToString();
			Log.Debug("NAME: " + name);
			if (!name.Contains("wled", StringComparison.InvariantCulture) && !name.Contains("arduino")) {
				return;
			}

			var rr = e.Message.AdditionalRecords;

			foreach (var id in from msg in rr where msg.Type == DnsType.TXT select msg.CanonicalName.Split(".")[0]) {
				var nData = new WledData(id);
				_controlService.AddDevice(nData).ConfigureAwait(false);
			}

			if (_stopDiscovery) {
				_discovering = false;
			}
		}
	}
}