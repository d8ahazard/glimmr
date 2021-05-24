using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Services;
using Makaretu.Dns;
using Newtonsoft.Json;
using Serilog;
using IPAddress = Org.BouncyCastle.Utilities.Net.IPAddress;

namespace Glimmr.Models.ColorTarget.Wled {
	public class WledDiscovery : ColorDiscovery, IColorDiscovery {
		public override string DeviceTag { get; set; }
		private readonly ControlService _controlService;

		private readonly MulticastService _mDns;
		private readonly ServiceDiscovery _sd;
		private bool _discovering;
		private bool _stopDiscovery;
		private List<string> _ids;

		public WledDiscovery(ColorService cs) : base(cs) {
			_mDns = cs.ControlService.MulticastService;
			_controlService = cs.ControlService;
			_sd = _controlService.ServiceDiscovery;
			DeviceTag = "Wled";
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
				//_sd.QueryServiceInstances("_arduino._tcp");
				//_mDns.SendQuery("_arduino._tcp", type: DnsType.PTR);
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
			//_sd.QueryServiceInstances("_arduino._tcp");
		}

		private void WledDiscovered(object sender, ServiceInstanceDiscoveryEventArgs e) {
			var name = e.ServiceInstanceName.ToString();
			var foo = e.Message;
			if (!name.Contains("wled", StringComparison.InvariantCulture)) {
				return;
			}

			//Log.Debug("msg: " + foo);
			name = name.Split(".")[0];
			Log.Debug("Name: " + name);

			if (_ids.Contains(name)) {
				return;
			}

			try {
				var rr = e.Message.AdditionalRecords;
				var ip = string.Empty;
				var id = string.Empty;

				
				foreach (var msg in rr) {
					Log.Debug("Msg: " + msg.Name);
					// Extract IP
					if (msg.Type == DnsType.A) {
						ip = msg.ToString().Split(" ").Last();
					}

					// Extract Mac
					if (msg.Type == DnsType.TXT) {
						id = msg.ToString().Split("=")[1];
					}
				}
				Log.Debug("Creating new WLED...");
				if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(ip)) {
					var nData = new WledData(id, ip);

					Log.Debug("WLED found: " + JsonConvert.SerializeObject(nData));
					_controlService.AddDevice(nData).ConfigureAwait(false);
					_ids.Add(id);
				} else {
					Log.Warning("Unable to get data for wled.");
				}
			} catch (Exception p) {
				Log.Warning("Exception: " + p.Message);
			}

			

			if (_stopDiscovery) {
				_discovering = false;
			}
			
		}
	}
}