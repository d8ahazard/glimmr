#region

using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Makaretu.Dns;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Glimmr {
	public class GlimmrDiscovery : ColorDiscovery, IColorDiscovery {
		public override string DeviceTag { get; set; } = "Glimmr";
		private readonly ControlService _controlService;

		private readonly MulticastService _mDns;
		private readonly ServiceDiscovery _sd;
		private bool _stopDiscovery;

		public GlimmrDiscovery(ColorService cs) : base(cs) {
			_controlService = cs.ControlService;
			_mDns = _controlService.MulticastService;
			_sd = _controlService.ServiceDiscovery;
		}

		public async Task Discover(CancellationToken ct, int timeout) {
			Log.Debug("Glimmr: Discovery started...");
			try {
				_mDns.NetworkInterfaceDiscovered += InterfaceDiscovered;
				_sd.ServiceDiscovered += ServiceDiscovered;
				_sd.ServiceInstanceDiscovered += GlimmrDiscovered;
				_mDns.Start();
				await Task.Delay(TimeSpan.FromSeconds(timeout), CancellationToken.None);
				_mDns.NetworkInterfaceDiscovered -= InterfaceDiscovered;
				_sd.ServiceDiscovered -= ServiceDiscovered;
				_sd.ServiceInstanceDiscovered -= GlimmrDiscovered;
				_stopDiscovery = true;
				//_mDns.Stop();
			} catch {
				// Ignore collection modified exception
			}

			Log.Debug("Glimmr: Discovery complete.");
		}

		private void ServiceDiscovered(object? sender, DomainName serviceName) {
			if (!_stopDiscovery) {
				_mDns.SendQuery(serviceName, type: DnsType.PTR);
			}
		}

		private void InterfaceDiscovered(object? sender, NetworkInterfaceEventArgs e) {
			_sd.QueryServiceInstances("_glimmr._tcp");
		}

		private void GlimmrDiscovered(object sender, ServiceInstanceDiscoveryEventArgs e) {
			var name = e.ServiceInstanceName.ToString();
			if (!name.Contains("glimmr", StringComparison.InvariantCulture)) {
				return;
			}

			var rr = e.Message.AdditionalRecords;
			foreach (var msg in rr) {
				if (msg.Type == DnsType.A) {
					var ipString = msg.ToString().Split(" ").Last();
					var hostname = msg.CanonicalName.Split(".")[0];
					var ip = IPAddress.Parse(ipString);
					if (ip.ToString() != IpUtil.GetLocalIpAddress() && !string.Equals(hostname, Environment.MachineName,
						StringComparison.CurrentCultureIgnoreCase)) {
						var nData = new GlimmrData(hostname, ip);
						ControlService.AddDevice(nData).ConfigureAwait(false);
					}
				}
			}
		}
	}
}