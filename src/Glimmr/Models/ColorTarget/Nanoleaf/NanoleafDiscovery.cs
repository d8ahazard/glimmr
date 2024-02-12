#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Data;
using Glimmr.Models.Util;
using Glimmr.Services;
using Makaretu.Dns;
using Nanoleaf.Client;
using NetworkExtension;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Nanoleaf;

public class NanoleafDiscovery : ColorDiscovery, IColorDiscovery, IColorTargetAuth {
	private readonly ControlService _controlService;
	private readonly MulticastService _mDns;
	private readonly ServiceDiscovery _sd;
	private List<string> _ids;
	private bool _stopDiscovery;
	private static readonly List<string> _recordNames = new() {
		"_nanoleafapi._tcp"
	};

	public NanoleafDiscovery(ColorService cs) : base(cs) {
		_controlService = cs.ControlService;
		_mDns = _controlService.MulticastService;
		_sd = _controlService.ServiceDiscovery;
		_ids = new List<string>();
	}

	public async Task Discover(int timeout, CancellationToken ct) {
		_ids = new List<string>();
		Log.Information("Nanoleaf: Discovery started...");
		try {
			_mDns.NetworkInterfaceDiscovered += InterfaceDiscovered;
			_sd.ServiceDiscovered += ServiceDiscovered;
			_sd.ServiceInstanceDiscovered += DeviceDiscovered;
			_mDns.Start();
			foreach (var recordName in _recordNames) {
				_mDns.SendQuery(recordName, type: DnsType.PTR);
			}
			await Task.Delay(TimeSpan.FromSeconds(timeout), CancellationToken.None);
			_mDns.NetworkInterfaceDiscovered -= InterfaceDiscovered;
			_sd.ServiceDiscovered -= ServiceDiscovered;
			_sd.ServiceInstanceDiscovered -= DeviceDiscovered;
			_stopDiscovery = true;
		} catch {
			// Ignore collection modified exception
		}

		Log.Information("Nanoleaf: Discovery complete.");
	}

	private void ServiceDiscovered(object? sender, DomainName serviceName) {
		if (!_stopDiscovery) {
			var sendQuery = false;
			foreach(var _recordName in _recordNames) {
				if (serviceName.ToString().Contains(_recordName, StringComparison.OrdinalIgnoreCase)) {
					Log.Information("Service discovered: {ServiceName}", serviceName);
					sendQuery = true;
					break;
				}
			}
			if (sendQuery) {
				Log.Information("Service discovered: {ServiceName}", serviceName);
				_mDns.SendQuery(serviceName, type: DnsType.PTR);
			}
		}
	}

	public async Task<dynamic> CheckAuthAsync(dynamic deviceData) {
		var nanoleaf = new NanoleafClient(deviceData.IpAddress, "");
		try {
			var result = await nanoleaf.CreateTokenAsync();
			Log.Information("Authorized.");
			if (!string.IsNullOrEmpty(result.Token)) {
				deviceData.Token = result.Token;
				nanoleaf.Dispose();
				nanoleaf = new NanoleafClient(deviceData.IpAddress, result.Token);
				var layout = await nanoleaf.GetLayoutAsync();
				deviceData.Layout = new TileLayout(layout);
				Log.Information("New nano info: " + JsonConvert.SerializeObject(deviceData));
			}
		} catch (Exception e) {
			Log.Information("Nanoleaf Discovery Exception: " + e.Message);
		}

		nanoleaf.Dispose();
		return deviceData;
	}
	
	private void InterfaceDiscovered(object? sender, NetworkInterfaceEventArgs e) {
		Log.Information("Interface discovered: {NetworkInterface}", e.NetworkInterfaces);
		foreach(var _recordName in _recordNames) {
			_sd.QueryServiceInstances(_recordName);
		}
	}


	private void DeviceDiscovered(object? sender, ServiceInstanceDiscoveryEventArgs e) {
    // Check if the service instance name contains any of the record names we're interested in
    var doSend = _recordNames.Any(recordName => e.ServiceInstanceName.ToString().Contains(recordName, StringComparison.OrdinalIgnoreCase));
    if (!doSend) {
        return;
    }

    Log.Information("Nanoleaf device discovered: {ServiceInstanceName}", e.ServiceInstanceName);

    // Combine Answers and AdditionalRecords for processing
    var combinedRecords = e.Message.Answers.Concat(e.Message.AdditionalRecords).ToList();

    Log.Debug("Combined records for processing: {Records}", combinedRecords);

    if (!combinedRecords.Any()) {
        Log.Information("No records found, might need to query explicitly...");
        // Consider explicit querying if necessary, as previously discussed
        return;
    }

    var nData = ProcessDnsRecords(combinedRecords);

    if (nData == null) {
        Log.Warning("Failed to extract Nanoleaf data from DNS records.");
        return;
    }

    var existingData = DataUtil.GetDevice<NanoleafData>(nData.Id);
    if (existingData != null && !string.IsNullOrEmpty(existingData.Token)) {
        nData.Token = existingData.Token;
        UpdateNanoleafData(nData);
    }

    Log.Information("Processed Nanoleaf data: {NanoleafData}", JsonConvert.SerializeObject(nData));
    ControlService.AddDevice(nData).ConfigureAwait(true);
}

private NanoleafData? ProcessDnsRecords(IEnumerable<ResourceRecord> records) {
    var nData = new NanoleafData { IpAddress = string.Empty };
    foreach (var record in records) {
        Log.Information("Processing record: {Record}", record);
        switch (record) {
            case ARecord aRecord:
                nData.IpAddress = aRecord.Address.ToString();
                break;
            case TXTRecord txtRecord:
                ProcessTxtRecord(txtRecord, nData);
                break;
            case SRVRecord srvRecord:
                nData.Port = srvRecord.Port;
                nData.Hostname = srvRecord.Target.ToString();
                nData.Name = nData.Hostname.Replace(".local", "");
                break;
        }
    }

    if (string.IsNullOrEmpty(nData.Id) || string.IsNullOrEmpty(nData.IpAddress)) {
        return null;
    }

    return nData;
}

private void ProcessTxtRecord(TXTRecord txtRecord, NanoleafData nData) {
    foreach (var entry in txtRecord.Strings) {
        var parts = entry.Split('=');
        if (parts.Length != 2) continue;

        var key = parts[0].Trim();
        var value = parts[1].Trim();

        switch (key) {
            case "id":
                nData.Id = value;
                break;
            case "srcvers":
                nData.Version = value;
                break;
            case "md":
                nData.Type = value;
                break;
        }
    }
}

private void UpdateNanoleafData(NanoleafData nData) {
    try {
        var device = new NanoleafDevice(nData, _controlService.ColorService);
        var layout = device.GetLayout().Result; // Consider using async/await
        device.Dispose();
        if (layout != null) {
            nData.Layout = layout;
        }
    } catch (Exception ex) {
        Log.Error(ex, "Failed to update Nanoleaf data.");
    }
}



}