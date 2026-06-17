#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Services;
using Makaretu.Dns;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Twinkly;

public class TwinklyDiscovery : ColorDiscovery, IColorDiscovery {
	private readonly MulticastService _mDns;
	private readonly ServiceDiscovery _sd;
	private readonly List<string> _ids;
	private bool _stopDiscovery;
	private const string ServiceName = "_twinkly._tcp";

	public TwinklyDiscovery(ColorService cs) : base(cs) {
		var controlService = cs.ControlService;
		_mDns = controlService.MulticastService;
		_sd = controlService.ServiceDiscovery;
		_ids = new List<string>();
	}

	public async Task Discover(int timeout, CancellationToken ct) {
		Log.Debug("Twinkly: Discovery started...");
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

		Log.Debug("Twinkly: Discovery complete.");
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
		if (!e.Message.ToString().Contains("_twinkly")) return;

		var name = e.ServiceInstanceName.ToString();
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
			}
		} catch (Exception ex) {
			Log.Warning("Twinkly Discovery Exception: " + ex.Message);
		}
	}

	private static TwinklyData? ProbeDevice(string ip, string fallbackName) {
		try {
			using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
			var json = http.GetStringAsync($"http://{ip}/xled/v1/gestalt").Result;
			var gestalt = JsonConvert.DeserializeObject<TwinklyGestalt>(json);
			if (gestalt == null) return null;

			var deviceName = string.IsNullOrEmpty(gestalt.DeviceName) ? fallbackName : gestalt.DeviceName;
			var id = string.IsNullOrEmpty(gestalt.Mac) ? ip : gestalt.Mac;
			return new TwinklyData(id, ip, deviceName, gestalt.NumberOfLed);
		} catch (Exception ex) {
			Log.Debug("Twinkly: Could not probe device at " + ip + ": " + ex.Message);
			return null;
		}
	}
}

internal class TwinklyGestalt {
	[JsonProperty("product_name")]
	public string ProductName { get; set; } = "";

	[JsonProperty("device_name")]
	public string DeviceName { get; set; } = "";

	[JsonProperty("mac")]
	public string Mac { get; set; } = "";

	[JsonProperty("number_of_led")]
	public int NumberOfLed { get; set; }

	[JsonProperty("led_profile")]
	public string LedProfile { get; set; } = "";
}
