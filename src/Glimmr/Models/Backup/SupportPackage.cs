#region

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.Backup;

[Serializable]
public class SupportPackage {
	[JsonProperty]
	public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

	[JsonProperty]
	public string GlimmrVersion { get; set; } = "";

	[JsonProperty]
	public ConfigSummary ConfigSummary { get; set; } = new();

	[JsonProperty]
	public Dictionary<string, string> ProviderVersions { get; set; } = new();

	[JsonProperty]
	public List<string> DiscoveryLogs { get; set; } = new();

	[JsonProperty]
	public NetworkDiagnostics NetworkDiagnostics { get; set; } = new();

	[JsonProperty]
	public List<AnonymizedDeviceCapability> AnonymizedDeviceCapabilities { get; set; } = new();

	[JsonProperty]
	public PerformanceMetrics PerformanceMetrics { get; set; } = new();
}

[Serializable]
public class ConfigSummary {
	[JsonProperty]
	public int DeviceCount { get; set; }

	[JsonProperty]
	public int EnabledDeviceCount { get; set; }

	[JsonProperty]
	public int AmbientSceneCount { get; set; }

	[JsonProperty]
	public int AudioSceneCount { get; set; }

	[JsonProperty]
	public string DeviceMode { get; set; } = "";

	[JsonProperty]
	public int LedCount { get; set; }

	[JsonProperty]
	public string CaptureMode { get; set; } = "";

	[JsonProperty]
	public bool HasRoomMap { get; set; }
}

[Serializable]
public class NetworkDiagnostics {
	[JsonProperty]
	public List<NetworkInterfaceInfo> Interfaces { get; set; } = new();

	[JsonProperty]
	public bool MulticastAvailable { get; set; }

	[JsonProperty]
	public string LocalIpAddress { get; set; } = "";
}

[Serializable]
public class NetworkInterfaceInfo {
	[JsonProperty]
	public string Name { get; set; } = "";

	[JsonProperty]
	public string Type { get; set; } = "";

	[JsonProperty]
	public string Status { get; set; } = "";

	[JsonProperty]
	public string IpAddress { get; set; } = "";

	[JsonProperty]
	public bool SupportsMulticast { get; set; }
}

[Serializable]
public class AnonymizedDeviceCapability {
	[JsonProperty]
	public string Tag { get; set; } = "";

	[JsonProperty]
	public bool Enabled { get; set; }

	[JsonProperty]
	public int PropertyCount { get; set; }
}

[Serializable]
public class PerformanceMetrics {
	[JsonProperty]
	public float CpuUsage { get; set; }

	[JsonProperty]
	public float CpuTemp { get; set; }

	[JsonProperty]
	public float MemoryUsage { get; set; }

	[JsonProperty]
	public Dictionary<string, int> Fps { get; set; } = new();

	[JsonProperty]
	public string Uptime { get; set; } = "";
}
