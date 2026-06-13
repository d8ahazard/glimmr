using System.Text.Json.Serialization;

namespace GlimmrControl.Models;

/// <summary>The full datastore pushed by the hub via the "olo" message.</summary>
public class StoreData
{
	[JsonPropertyName("systemData")] public SystemData? SystemData { get; set; }
	[JsonPropertyName("devices")] public List<DeviceData>? Devices { get; set; }
	[JsonPropertyName("ambientScenes")] public List<Scene>? AmbientScenes { get; set; }
	[JsonPropertyName("audioScenes")] public List<Scene>? AudioScenes { get; set; }
	[JsonPropertyName("stats")] public StatData? Stats { get; set; }
}

/// <summary>System statistics (cpu, memory, fps...). Surfaced read-only in the control UI.</summary>
public class StatData
{
	[JsonPropertyName("cpuTemp")] public float CpuTemp { get; set; }
	[JsonPropertyName("cpuUsage")] public float CpuUsage { get; set; }
	[JsonPropertyName("memoryUsage")] public float MemoryUsage { get; set; }
	[JsonPropertyName("uptime")] public string? Uptime { get; set; }
}

/// <summary>Emulated Hue bridge pairing status (GET /api/Glimmr/hueSyncStatus).</summary>
public class HueSyncStatus
{
	[JsonPropertyName("bridgeId")] public string? BridgeId { get; set; }
	[JsonPropertyName("bridgeMac")] public string? BridgeMac { get; set; }
	[JsonPropertyName("armed")] public bool Armed { get; set; }
	[JsonPropertyName("paired")] public bool Paired { get; set; }
	[JsonPropertyName("streaming")] public bool Streaming { get; set; }
}
