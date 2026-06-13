using System.Text.Json;
using System.Text.Json.Serialization;

namespace GlimmrControl.Models;

/// <summary>
///     A Glimmr color-target device (LED strip, Hue bridge, WLED, ...). Mirrors the camelCase
///     DeviceData JSON. The <c>tag</c> is required by the hub's UpdateDevice to resolve the
///     concrete server type, so all unmodeled fields are preserved through <see cref="Extra" />.
/// </summary>
public class DeviceData
{
	[JsonPropertyName("id")] public string Id { get; set; } = "";
	[JsonPropertyName("name")] public string Name { get; set; } = "";
	[JsonPropertyName("tag")] public string Tag { get; set; } = "";
	[JsonPropertyName("deviceTag")] public string? DeviceTag { get; set; }
	[JsonPropertyName("ipAddress")] public string IpAddress { get; set; } = "";
	[JsonPropertyName("enable")] public bool Enable { get; set; }
	[JsonPropertyName("brightness")] public int Brightness { get; set; }
	[JsonPropertyName("maxBrightness")] public int? MaxBrightness { get; set; }
	[JsonPropertyName("lastSeen")] public string? LastSeen { get; set; }

	[JsonExtensionData]
	public Dictionary<string, JsonElement> Extra { get; set; } = new();
}
