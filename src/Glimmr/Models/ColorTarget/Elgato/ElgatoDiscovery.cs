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

namespace Glimmr.Models.ColorTarget.Elgato;

public class ElgatoDiscovery : ColorDiscovery, IColorDiscovery {
	private readonly MulticastService _mDns;
	private readonly ServiceDiscovery _sd;
	private readonly List<string> _ids;
	private bool _stopDiscovery;
	private const string ServiceName = "_elg._tcp";

	public ElgatoDiscovery(ColorService cs) : base(cs) {
		var controlService = cs.ControlService;
		_mDns = controlService.MulticastService;
		_sd = controlService.ServiceDiscovery;
		_ids = new List<string>();
	}

	public async Task Discover(int timeout, CancellationToken ct) {
		Log.Debug("Elgato: Discovery started...");
		try {
			_stopDiscovery = false;
			_ids.Clear();
			_mDns.NetworkInterfaceDiscovered += InterfaceDiscovered;
			_sd.ServiceDiscovered += ServiceDiscovered;
			_sd.ServiceInstanceDiscovered += DeviceDiscovered;
			_mDns.Start();
			// Use the fully-qualified query ("<service>.local"); a bare "_elg._tcp" PTR query is
			// ignored by real mDNS responders.
			_sd.QueryServiceInstances(ServiceName);
			await Task.Delay(TimeSpan.FromSeconds(timeout), CancellationToken.None);
			_mDns.NetworkInterfaceDiscovered -= InterfaceDiscovered;
			_sd.ServiceDiscovered -= ServiceDiscovered;
			_sd.ServiceInstanceDiscovered -= DeviceDiscovered;
			_stopDiscovery = true;
		} catch {
			// Ignore collection modified exception
		}

		Log.Debug("Elgato: Discovery complete.");
	}

	private void ServiceDiscovered(object? sender, DomainName serviceName) {
		if (!_stopDiscovery && serviceName.ToString().Contains(ServiceName)) {
			_mDns.SendQuery(serviceName, type: DnsType.PTR);
		}
	}

	private void InterfaceDiscovered(object? sender, NetworkInterfaceEventArgs e) {
		if (_stopDiscovery) return;
		_sd.QueryServiceInstances(ServiceName);
	}

	private void DeviceDiscovered(object? sender, ServiceInstanceDiscoveryEventArgs e) {
		if (!e.Message.ToString().Contains("_elg")) return;

		var name = e.ServiceInstanceName.ToString();
		if (name.Contains(".local")) {
			name = name.Split(".")[0];
		}

		if (_ids.Contains(name)) return;

		try {
			var combinedRecords = e.Message.Answers.Concat(e.Message.AdditionalRecords).ToList();
			var ip = string.Empty;
			var id = string.Empty;

			foreach (var msg in combinedRecords) {
				switch (msg.Type) {
					case DnsType.A:
						ip = msg.ToString().Split(" ").Last();
						break;
					case DnsType.TXT:
						id = msg.ToString().Split(" ").Last();
						break;
				}
			}

			if (string.IsNullOrEmpty(ip)) return;
			if (string.IsNullOrEmpty(id)) id = ip;

			var data = new ElgatoData(id, ip, name);
			ControlService.AddDevice(data).ConfigureAwait(false);
			_ids.Add(name);
			Log.Debug("Elgato: Found device at " + ip);
		} catch (Exception ex) {
			Log.Warning("Elgato Discovery Exception: " + ex.Message);
		}
	}
}
