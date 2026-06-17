#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Services;
using Makaretu.Dns;
using Newtonsoft.Json.Linq;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Shelly;

public class ShellyDiscovery : ColorDiscovery, IColorDiscovery {
	private readonly MulticastService _mDns;
	private readonly ServiceDiscovery _sd;
	private readonly List<string> _ids;
	private bool _stopDiscovery;
	private const string ServiceName = "_http._tcp";

	public ShellyDiscovery(ColorService cs) : base(cs) {
		var controlService = cs.ControlService;
		_mDns = controlService.MulticastService;
		_sd = controlService.ServiceDiscovery;
		_ids = new List<string>();
	}

	public async Task Discover(int timeout, CancellationToken ct) {
		Log.Debug("Shelly: Discovery started...");
		try {
			_stopDiscovery = false;
			_ids.Clear();
			_mDns.NetworkInterfaceDiscovered += InterfaceDiscovered;
			_sd.ServiceDiscovered += ServiceDiscovered;
			_sd.ServiceInstanceDiscovered += DeviceDiscovered;
			_mDns.Start();
			// Use the fully-qualified query ("<service>.local"); a bare PTR query without the
			// ".local" suffix is ignored by real mDNS responders.
			_sd.QueryServiceInstances(ServiceName);
			await Task.Delay(TimeSpan.FromSeconds(timeout), CancellationToken.None);
			_mDns.NetworkInterfaceDiscovered -= InterfaceDiscovered;
			_sd.ServiceDiscovered -= ServiceDiscovered;
			_sd.ServiceInstanceDiscovered -= DeviceDiscovered;
			_stopDiscovery = true;
		} catch {
			// Ignore collection modified exception
		}

		Log.Debug("Shelly: Discovery complete.");
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
		var name = e.ServiceInstanceName.ToString();
		if (!name.Contains("shelly", StringComparison.OrdinalIgnoreCase)) return;

		if (name.Contains(".local")) {
			name = name.Split(".")[0];
		}

		if (_ids.Contains(name)) return;

		try {
			var combinedRecords = e.Message.Answers.Concat(e.Message.AdditionalRecords).ToList();
			var ip = string.Empty;

			foreach (var msg in combinedRecords.Where(msg => msg.Type == DnsType.A)) {
				ip = msg.ToString().Split(" ").Last();
				break;
			}

			if (string.IsNullOrEmpty(ip)) return;

			var data = ProbeDevice(ip, name);
			if (data != null) {
				ControlService.AddDevice(data).ConfigureAwait(false);
				_ids.Add(name);
				Log.Debug("Shelly: Found device at " + ip);
			}
		} catch (Exception ex) {
			Log.Warning("Shelly Discovery Exception: " + ex.Message);
		}
	}

	private static ShellyData? ProbeDevice(string ip, string fallbackName) {
		try {
			using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
			var json = http.GetStringAsync($"http://{ip}/rpc/Shelly.GetStatus").Result;
			var obj = JObject.Parse(json);

			var deviceName = fallbackName;
			var mac = obj["sys"]?["mac"]?.ToString() ?? ip;

			return new ShellyData(mac, ip, deviceName);
		} catch (Exception ex) {
			Log.Debug("Shelly: Could not probe device at " + ip + ": " + ex.Message);
			return null;
		}
	}
}
