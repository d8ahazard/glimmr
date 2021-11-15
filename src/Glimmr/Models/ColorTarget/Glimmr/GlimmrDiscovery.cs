#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Makaretu.Dns;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Glimmr {
	public class GlimmrDiscovery : ColorDiscovery, IColorDiscovery {
		private readonly MulticastService _mDns;
		private readonly ServiceDiscovery _sd;
		private List<string> _ids;
		private bool _stopDiscovery;

		public GlimmrDiscovery(ColorService cs) : base(cs) {
			var controlService = cs.ControlService;
			_mDns = controlService.MulticastService;
			_sd = controlService.ServiceDiscovery;
			_ids = new List<string>();
		}

		public async Task Discover(CancellationToken ct, int timeout) {
			_ids = new List<string>();
			Log.Debug("Glimmr: Discovery started...");
			try {
				_mDns.NetworkInterfaceDiscovered += InterfaceDiscovered;
				_sd.ServiceDiscovered += ServiceDiscovered;
				_sd.ServiceInstanceDiscovered += DeviceDiscovered;
				_mDns.Start();
				_mDns.SendQuery("_glimmr._tcp", type: DnsType.PTR);
				await Task.Delay(TimeSpan.FromSeconds(timeout), CancellationToken.None);
				_mDns.NetworkInterfaceDiscovered -= InterfaceDiscovered;
				_sd.ServiceDiscovered -= ServiceDiscovered;
				_sd.ServiceInstanceDiscovered -= DeviceDiscovered;
				_stopDiscovery = true;
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

		private void DeviceDiscovered(object? sender, ServiceInstanceDiscoveryEventArgs e) {
			var foo = e.Message;
			if (!foo.ToString().Contains("_glimmr")) {
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
							Log.Debug("A: " + msg);
							ip = msg.ToString().Split(" ").Last();
							break;
						// Extract Mac
						case DnsType.TXT:
							Log.Debug("TXT: " + msg);
							id = msg.ToString().Split("mac=")[1];
							break;
					}
				}

				if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(ip)) {
					var sd = DataUtil.GetSystemData();
					if (id == sd.DeviceId) {
						return;
					}

					if (name == id) {
						name = "Glimmr-" + id[..3];
					}

					Log.Debug($"Adding glimmr: {id}:{ip}-{name}");
					var nData = new GlimmrData(id, ip) { Name = name };
					ControlService.AddDevice(nData).ConfigureAwait(false);
					_ids.Add(id);
				} else {
					Log.Warning("Unable to get data for Glimmr.");
				}
			} catch (Exception p) {
				Log.Warning("Glimmr Discovery Exception: " + p.Message);
			}
		}
	}
}