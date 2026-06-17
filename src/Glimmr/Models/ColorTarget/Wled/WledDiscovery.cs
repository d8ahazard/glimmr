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
	private readonly HashSet<string> _ids;
	private readonly object _idLock = new();
	private bool _stopDiscovery;
	private const string _serviceName = "_wled._tcp";

	public WledDiscovery(ColorService cs) : base(cs) {
		var controlService = cs.ControlService;
		_mDns = controlService.MulticastService;
		_sd = controlService.ServiceDiscovery;
		_ids = new HashSet<string>();
	}

	public async Task Discover(int timeout, CancellationToken ct) {
		lock (_idLock) {
			_ids.Clear();
		}

		Log.Debug("WLED: Discovery started...");

		try {
			_mDns.NetworkInterfaceDiscovered += InterfaceDiscovered;
			_sd.ServiceDiscovered += ServiceDiscovered;
			_sd.ServiceInstanceDiscovered += DeviceDiscovered;
			_mDns.Start();
			// Query the fully-qualified service instance. Makaretu's QueryServiceInstances
			// appends ".local"; a bare "_wled._tcp" PTR query (what we sent before) is ignored
			// by real WLED/ESP mDNS responders - they only answer "_wled._tcp.local" - so no
			// device was ever discovered. (Glimmr's own devices answered the bare query only
			// because they run the same lenient Makaretu responder.)
			// Re-issue the query every couple of seconds across the window. mDNS multicast is
			// lossy on WiFi, so cheap ESP boards (e.g. ESP32-C3) often miss or drop a single
			// query - repeating it greatly improves the odds of catching every device.
			var reps = Math.Max(1, timeout / 2);
			for (var i = 0; i < reps && !ct.IsCancellationRequested; i++) {
				_sd.QueryServiceInstances(_serviceName);
				await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None);
			}

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

		// Claim this instance up front so the repeated mDNS queries (and multi-interface
		// answers) don't spawn concurrent /json fetches to the same device - cheap ESP boards
		// can't service parallel requests and time out when hammered.
		lock (_idLock) {
			if (!_ids.Add(name)) {
				Log.Debug("WLED: Device already being handled. Ignoring: " + name);
				return;
			}
		}

		try {
			var combinedRecords = e.Message.Answers.Concat(e.Message.AdditionalRecords).ToList();
			var ip = string.Empty;
			var id = string.Empty;

			// Use the strongly-typed records rather than string-splitting ToString(): WLED's TXT
			// record is "mac=<hex>", which the old Split(" ").Last() turned into an id of
			// "mac=..." (creating duplicates). The A record gives us the IPv4 address directly.
			foreach (var msg in combinedRecords) {
				switch (msg) {
					case ARecord a:
						ip = a.Address.ToString();
						Log.Debug("WLED: Found IP: " + ip + " for " + name);
						break;
					case TXTRecord t: {
						foreach (var s in t.Strings) {
							if (!s.StartsWith("mac=", StringComparison.OrdinalIgnoreCase)) {
								continue;
							}

							id = s["mac=".Length..].Trim();
							Log.Debug("WLED: Found ID: " + id + " for " + name);
						}

						break;
					}
				}
			}

			// Fall back to the mDNS instance name (e.g. "wled-e3b958") if no MAC was advertised,
			// so a device is still created.
			if (string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name)) {
				id = name;
				Log.Debug("WLED: No MAC in TXT; using instance name as ID: " + id);
			}

			if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(ip)) {
				try {
					var nData = new WledData(id, ip);
					if (nData.Initialized) {
						Log.Debug("WLED: Adding device to control service. ID: " + id + " IP: " + ip + "...");
						ControlService.AddDevice(nData).ConfigureAwait(false);
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