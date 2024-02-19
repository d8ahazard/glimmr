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

namespace Glimmr.Models.ColorTarget.Wled;

public class WledDiscovery : ColorDiscovery, IColorDiscovery {
	private readonly MulticastService _mDns;
	private readonly ServiceDiscovery _sd;
	private List<string> _ids;
	private bool _stopDiscovery;
	private const string _serviceName = "_wled._tcp";

	public WledDiscovery(ColorService cs) : base(cs) {
		var controlService = cs.ControlService;
		_mDns = controlService.MulticastService;
		_sd = controlService.ServiceDiscovery;
		_ids = new List<string>();
	}

	public async Task Discover(int timeout, CancellationToken ct) {
		_ids = new List<string>();
		Log.Debug("WLED: Discovery started...");

		try {
			_mDns.NetworkInterfaceDiscovered += InterfaceDiscovered;
			_sd.ServiceDiscovered += ServiceDiscovered;
			_sd.ServiceInstanceDiscovered += DeviceDiscovered;
			_mDns.Start();
			_mDns.SendQuery(_serviceName, type: DnsType.PTR);
			await Task.Delay(TimeSpan.FromSeconds(timeout), CancellationToken.None);
			_mDns.NetworkInterfaceDiscovered -= InterfaceDiscovered;
			_sd.ServiceDiscovered -= ServiceDiscovered;
			_sd.ServiceInstanceDiscovered -= DeviceDiscovered;
			_stopDiscovery = true;
		} catch {
			// Ignore collection modified exception
		}

		Log.Debug("WLED: Discovery complete.");
	}

	private void ServiceDiscovered(object? sender, DomainName serviceName) {
		if (!_stopDiscovery && serviceName.ToString().Contains(_serviceName)) {
			Log.Debug("WLED: Service discovered. Sending query...");
			_mDns.SendQuery(serviceName, type: DnsType.PTR);
		}
	}

	private void InterfaceDiscovered(object? sender, NetworkInterfaceEventArgs e) {
		if (_stopDiscovery) return;
		Log.Debug("WLED: Interface discovered. Sending query...");
		_sd.QueryServiceInstances(_serviceName);
	}

	private void DeviceDiscovered(object? sender, ServiceInstanceDiscoveryEventArgs e) {
		var foo = e.Message;
		var name = e.ServiceInstanceName.ToString();

		if (!foo.ToString().Contains("_wled")) {
			return;
		}

		
		if (name.Contains(".local")) {
			name = name.Split(".")[0];
		}

		if (_ids.Contains(name)) {
			Log.Debug("WLED: Device already discovered. Ignoring: " + name);
			return;
		}

		try {
			var combinedRecords = e.Message.Answers.Concat(e.Message.AdditionalRecords).ToList();
			var ip = string.Empty;
			var id = string.Empty;


			foreach (var msg in combinedRecords.Where(msg => msg.Type is DnsType.A or DnsType.TXT)) {
				switch (msg.Type) {
					case DnsType.A:
						ip = msg.ToString().Split(" ").Last();
						Log.Debug("WLED: Found IP: " + ip + " for " + name);
						break;
					case DnsType.TXT:
						id = msg.ToString().Split(" ").Last();
						Log.Debug("WLED: Found ID: " + id + " for " + name);
						break;
					default:
						Log.Debug("WLED: Unknown record type. Ignoring: " + msg.Type + " for " + name);
						continue;
				}
			}

			if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(ip)) {
				try {
					var nData = new WledData(id, ip);
					if (nData.Initialized) {
						Log.Debug("WLED: Adding device to control service. ID: " + id + " IP: " + ip + "...");
						ControlService.AddDevice(nData).ConfigureAwait(false);
						_ids.Add(id);	
					}
				} catch (Exception f) {
					Log.Warning("WLED: Exception creating WLED data: " + f.Message);
					
				}

				
				
			} else {
				Log.Warning("Unable to get data for wled.");
			}
		} catch (Exception p) {
			Log.Warning("WLED Discovery Exception: " + p.Message);
		}
	}
}