#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Services;
using Makaretu.Dns;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Wled {
	public class WledDiscovery : ColorDiscovery, IColorDiscovery {
		public override string DeviceTag { get; set; } = "Wled";
		private readonly ControlService _controlService;

		private readonly MulticastService _mDns;
		private readonly ServiceDiscovery _sd;
		private List<string> _ids;
		private bool _stopDiscovery;

		public WledDiscovery(ColorService cs) : base(cs) {
			_mDns = cs.ControlService.MulticastService;
			_controlService = cs.ControlService;
			_sd = _controlService.ServiceDiscovery;
			_ids = new List<string>();
		}

		public async Task Discover(CancellationToken ct, int timeout) {
			_ids = new List<string>();
			Log.Debug("WLED: Discovery started...");

			try {
				_mDns.Start();
				_mDns.NetworkInterfaceDiscovered += InterfaceDiscovered;
				_sd.ServiceDiscovered += ServiceDiscovered;
				_sd.ServiceInstanceDiscovered += WledDiscovered;
				_sd.QueryServiceInstances("_wled._tcp");
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
			if (!_stopDiscovery) {
				_mDns.SendQuery(serviceName, type: DnsType.PTR);
			}
		}

		private void InterfaceDiscovered(object? sender, NetworkInterfaceEventArgs e) {
			_sd.QueryServiceInstances("_wled._tcp");
			//_sd.QueryServiceInstances("_arduino._tcp");
		}

		private void WledDiscovered(object? sender, ServiceInstanceDiscoveryEventArgs e) {
			var foo = e.Message;
			if (!foo.ToString().Contains("_wled")) {
				return;
			}

			var name = e.ServiceInstanceName.ToString();

			if (name.Contains(".local")) {
				name = name.Split(".")[0];
			}
			
			if (_ids.Contains(name)) {
				return;
			}

			try {
				var rr = e.Message.AdditionalRecords;
				var ip = string.Empty;
				var id = string.Empty;


				foreach (var msg in rr) {
					switch (msg.Type) {
						// Extract IP
						case DnsType.A:
							ip = msg.ToString().Split(" ").Last();
							break;
						// Extract Mac
						case DnsType.TXT:
							id = msg.ToString().Split("=")[1];
							break;
					}
				}

				if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(ip)) {
					var nData = new WledData(id, ip) {Name = name};
					ControlService.AddDevice(nData).ConfigureAwait(false);
					_ids.Add(id);
				} else {
					Log.Warning("Unable to get data for wled.");
				}
			} catch (Exception p) {
				Log.Warning("WLED Discovery Exception: " + p.Message);
			}
		}
	}
}