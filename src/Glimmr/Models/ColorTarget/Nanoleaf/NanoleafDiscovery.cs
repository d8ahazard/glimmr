#region

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Makaretu.Dns;
using Nanoleaf.Client;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Nanoleaf;

public class NanoleafDiscovery : ColorDiscovery, IColorDiscovery, IColorTargetAuth {
	private readonly ControlService _controlService;
	private readonly MulticastService _mDns;
	private readonly ServiceDiscovery _sd;

	public NanoleafDiscovery(ColorService cs) : base(cs) {
		_controlService = cs.ControlService;
		_mDns = _controlService.MulticastService;
		_sd = _controlService.ServiceDiscovery;
	}

	public async Task Discover(CancellationToken ct, int timeout) {
		_mDns.NetworkInterfaceDiscovered += InterfaceDiscovered;
		_sd.ServiceDiscovered += ServiceDiscovered;
		_sd.ServiceInstanceDiscovered += NanoleafDiscovered;
		_mDns.Start();
		Log.Debug("Nano: Discovery started...");
		await Task.Delay(TimeSpan.FromSeconds(timeout), CancellationToken.None);
		_mDns.NetworkInterfaceDiscovered -= InterfaceDiscovered;
		_sd.ServiceDiscovered -= ServiceDiscovered;
		_sd.ServiceInstanceDiscovered -= NanoleafDiscovered;
		//_mDns.Stop();
		Log.Debug("Nano: Discovery complete.");
	}

	public async Task<dynamic> CheckAuthAsync(dynamic deviceData) {
		var nanoleaf = new NanoleafClient(deviceData.IpAddress, "");
		try {
			var result = await nanoleaf.CreateTokenAsync();
			Log.Debug("Authorized.");
			if (!string.IsNullOrEmpty(result.Token)) {
				deviceData.Token = result.Token;
				nanoleaf.Dispose();
				nanoleaf = new NanoleafClient(deviceData.IpAddress, result.Token);
				var layout = await nanoleaf.GetLayoutAsync();
				deviceData.Layout = new TileLayout(layout);
				Log.Debug("New nano info: " + JsonConvert.SerializeObject(deviceData));
			}
		} catch (Exception e) {
			Log.Debug("Nanoleaf Discovery Exception: " + e.Message);
		}

		nanoleaf.Dispose();
		return deviceData;
	}

	private void InterfaceDiscovered(object? sender, NetworkInterfaceEventArgs e) {
		_sd.QueryServiceInstances("_nanoleafapi._tcp");
	}

	private void ServiceDiscovered(object? sender, DomainName serviceName) {
		_mDns.SendQuery(serviceName, type: DnsType.PTR);
	}

	private void NanoleafDiscovered(object? sender, ServiceInstanceDiscoveryEventArgs e) {
		var name = e.ServiceInstanceName.ToString();
		var nData = new NanoleafData { IpAddress = string.Empty };
		if (!name.Contains("nanoleafapi", StringComparison.InvariantCulture)) {
			return;
		}

		var devName = name.Split(".")[0];
		devName = devName[Math.Max(0, devName.Length - 3)..];
		nData.Name = devName;
		foreach (var msg in e.Message.AdditionalRecords) {
			try {
				switch (msg.Type) {
					case DnsType.A:
						var aString = msg.ToString();
						var aValues = aString.Split(" ");
						nData.IpAddress = aValues[4];
						break;
					case DnsType.TXT:
						var txtString = msg.ToString();
						var txtValues = txtString.Split(" ");
						nData.Version = txtValues[5]
							.Replace("srcvers=", string.Empty, StringComparison.InvariantCulture);
						nData.Type = txtValues[4].Replace("md=", string.Empty, StringComparison.InvariantCulture);
						nData.Id = txtValues[3].Replace("id=", string.Empty, StringComparison.InvariantCulture);
						break;
					case DnsType.SRV:
						var sString = msg.ToString();
						var sValues = sString.Split(" ");
						nData.Port = int.Parse(sValues[6], CultureInfo.InvariantCulture);
						nData.Hostname = sValues[7];
						break;
				}
			} catch (Exception f) {
				Log.Debug("Exception caught: " + f.Message);
			}
		}

		nData.Name = nData.Type switch {
			"NL42" => $"Shape {devName}",
			"NL29" => $"Canvas {devName}",
			"NL22" => $"Rhythm {devName}",
			_ => nData.Name
		};

		if (string.IsNullOrEmpty(nData.IpAddress) && !string.IsNullOrEmpty(nData.Hostname)) {
			nData.IpAddress = nData.Hostname;
		}

		NanoleafData? ex = DataUtil.GetDevice<NanoleafData>(nData.Id);
		if (ex != null) {
			if (!string.IsNullOrEmpty(ex.Token)) {
				nData.Token = ex.Token;
				try {
					var nd = new NanoleafDevice(nData, _controlService.ColorService);
					var layout = nd.GetLayout().Result;
					nd.Dispose();
					if (layout != null) {
						nData.Layout = layout;
					}
				} catch (Exception f) {
					Log.Debug("Exception: " + f.Message);
				}
			}
		}

		ControlService.AddDevice(nData).ConfigureAwait(true);
	}
}