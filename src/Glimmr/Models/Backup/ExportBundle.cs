#region

using System;
using System.Collections.Generic;
using Glimmr.Models.ColorSource.Ambient;
using Glimmr.Models.ColorSource.Audio;
using Glimmr.Models.FullSpectrum;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.Backup;

[Serializable]
public class ExportBundle {
	[JsonProperty]
	public string Version { get; set; } = "1.0";

	[JsonProperty]
	public DateTime Timestamp { get; set; } = DateTime.UtcNow;

	[JsonProperty]
	public string GlimmrVersion { get; set; } = "";

	[JsonProperty]
	public ExportManifest Manifest { get; set; } = new();

	[JsonProperty]
	public List<dynamic> Devices { get; set; } = new();

	[JsonProperty]
	public AmbientScene[] AmbientScenes { get; set; } = Array.Empty<AmbientScene>();

	[JsonProperty]
	public AudioScene[] AudioScenes { get; set; } = Array.Empty<AudioScene>();

	[JsonProperty]
	public Dictionary<string, object?> SourceSettings { get; set; } = new();

	[JsonProperty]
	public EmitterGraph? RoomMap { get; set; }

	[JsonProperty]
	public List<OutputSchedulerProfile> LatencyProfiles { get; set; } = new();

	[JsonProperty]
	public List<string> CompatibilityNotes { get; set; } = new();

	[JsonProperty]
	public Dictionary<string, string> EncryptedSecrets { get; set; } = new();
}

[Serializable]
public class ExportManifest {
	[JsonProperty]
	public string[] ProviderIds { get; set; } = Array.Empty<string>();

	[JsonProperty]
	public int DeviceCount { get; set; }

	[JsonProperty]
	public int SceneCount { get; set; }

	[JsonProperty]
	public bool HasRoomMap { get; set; }

	[JsonProperty]
	public string ExportedBy { get; set; } = "";

	[JsonProperty]
	public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
}

[Serializable]
public class ImportResult {
	[JsonProperty]
	public bool Success { get; set; }

	[JsonProperty]
	public int MigratedDevices { get; set; }

	[JsonProperty]
	public int SkippedDevices { get; set; }

	[JsonProperty]
	public List<string> Warnings { get; set; } = new();

	[JsonProperty]
	public List<string> Errors { get; set; } = new();
}
