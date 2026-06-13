using System.Text.Json;
using System.Text.Json.Serialization;

namespace GlimmrControl.Models;

/// <summary>
///     Mirrors the camelCase SystemData JSON pushed by the Glimmr SignalR hub ("olo" store).
///     Only the fields the app reads or writes are strongly typed; everything else is preserved
///     verbatim through <see cref="Extra" /> so updates never clobber server-only settings.
/// </summary>
public class SystemData
{
	[JsonPropertyName("deviceName")] public string DeviceName { get; set; } = "";
	[JsonPropertyName("ipAddress")] public string IpAddress { get; set; } = "";
	[JsonPropertyName("version")] public string Version { get; set; } = "";
	[JsonPropertyName("theme")] public string Theme { get; set; } = "dark";

	[JsonPropertyName("deviceId")] public string? DeviceId { get; set; }
	[JsonPropertyName("deviceMode")] public int DeviceMode { get; set; }
	[JsonPropertyName("autoDisabled")] public bool AutoDisabled { get; set; }

	// Ambient
	[JsonPropertyName("ambientScene")] public int AmbientScene { get; set; }
	[JsonPropertyName("ambientColor")] public string AmbientColor { get; set; } = "FFFFFF";

	// Audio
	[JsonPropertyName("audioScene")] public int AudioScene { get; set; }
	[JsonPropertyName("audioGain")] public float AudioGain { get; set; }
	[JsonPropertyName("audioRotationLower")] public float AudioRotationLower { get; set; }
	[JsonPropertyName("audioRotationUpper")] public float AudioRotationUpper { get; set; } = 1f;
	[JsonPropertyName("audioRotationSpeed")] public float AudioRotationSpeed { get; set; }
	[JsonPropertyName("audioRotationTrigger")] public int AudioRotationTrigger { get; set; }

	// Streaming
	[JsonPropertyName("streamMode")] public int StreamMode { get; set; }
	[JsonPropertyName("dsIp")] public string DsIp { get; set; } = "";

	// Hue sync (read-only helpers, surfaced in the control screen)
	[JsonPropertyName("hueSyncBridgeId")] public string? HueSyncBridgeId { get; set; }
	[JsonPropertyName("hueSyncBridgeMac")] public string? HueSyncBridgeMac { get; set; }

	/// <summary>All other server fields, preserved untouched across round-trips.</summary>
	[JsonExtensionData]
	public Dictionary<string, JsonElement> Extra { get; set; } = new();
}
