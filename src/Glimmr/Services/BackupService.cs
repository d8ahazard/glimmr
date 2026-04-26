#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Glimmr.Models.Backup;
using Glimmr.Models.ColorSource.Ambient;
using Glimmr.Models.ColorSource.Audio;
using Glimmr.Models.ColorTarget;
using Glimmr.Models.Data;
using Glimmr.Models.FullSpectrum;
using Glimmr.Models.Helper;
using Glimmr.Models.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

#endregion

namespace Glimmr.Services;

public class BackupService {
	private static readonly string[] SecretPropertyNames = {
		"Token", "AppKey", "User", "AccessToken", "Secret", "ApiKey", "Password"
	};

	private readonly ControlService _controlService;

	public BackupService(ControlService controlService) {
		_controlService = controlService;
	}

	public ExportBundle CreateExport() {
		var sd = DataUtil.GetSystemData();
		var devices = DataUtil.GetDevices();

		var jl1 = new JsonLoader("ambientScenes");
		var jl2 = new JsonLoader("audioScenes");
		var ambientScenes = jl1.LoadDynamic<AmbientScene>().ToArray();
		var audioScenes = jl2.LoadDynamic<AudioScene>().ToArray();

		var roomMap = DataUtil.GetObject<EmitterGraph>("EmitterGraph");
		var latencyProfiles = DataUtil.GetCollection<OutputSchedulerProfile>("OutputSchedulerProfile") ?? new List<OutputSchedulerProfile>();

		var providerIds = ExtractProviderIds(devices);

		var serializedDevices = new List<dynamic>();
		var secrets = new Dictionary<string, string>();
		foreach (var device in devices) {
			try {
				var json = JsonConvert.SerializeObject(device);
				var jObj = JObject.Parse(json);
				var deviceId = jObj["Id"]?.ToString() ?? Guid.NewGuid().ToString();

				RedactSecrets(jObj, deviceId, secrets);

				serializedDevices.Add(jObj);
			} catch (Exception e) {
				Log.Warning($"Error serializing device for export: {e.Message}");
			}
		}

		var sourceSettings = new Dictionary<string, object?>();
		try {
			sourceSettings["SystemData"] = sd;
		} catch (Exception e) {
			Log.Warning($"Error capturing source settings: {e.Message}");
		}

		var bundle = new ExportBundle {
			Version = "1.0",
			Timestamp = DateTime.UtcNow,
			GlimmrVersion = sd.Version,
			Devices = serializedDevices,
			AmbientScenes = ambientScenes,
			AudioScenes = audioScenes,
			SourceSettings = sourceSettings,
			RoomMap = roomMap != null ? (EmitterGraph)roomMap : null,
			LatencyProfiles = latencyProfiles,
			EncryptedSecrets = secrets,
			Manifest = new ExportManifest {
				ProviderIds = providerIds,
				DeviceCount = devices.Count,
				SceneCount = ambientScenes.Length + audioScenes.Length,
				HasRoomMap = roomMap != null,
				ExportedBy = sd.DeviceName,
				ExportedAt = DateTime.UtcNow
			}
		};

		bundle.CompatibilityNotes.Add($"Exported from Glimmr {sd.Version}");
		bundle.CompatibilityNotes.Add($"Platform: {RuntimeInformation.OSDescription}");

		return bundle;
	}

	public ImportResult ImportExport(ExportBundle bundle) {
		var result = new ImportResult();

		if (bundle.Manifest == null!) {
			result.Errors.Add("Bundle manifest is missing.");
			return result;
		}

		if (string.IsNullOrEmpty(bundle.Version)) {
			result.Warnings.Add("Bundle version is empty, assuming v1.0 format.");
		}

		var sd = DataUtil.GetSystemData();
		if (!string.IsNullOrEmpty(bundle.GlimmrVersion) && bundle.GlimmrVersion != sd.Version) {
			result.Warnings.Add($"Version mismatch: bundle={bundle.GlimmrVersion}, current={sd.Version}. Some settings may need adjustment.");
		}

		foreach (var deviceObj in bundle.Devices) {
			try {
				JObject jObj;
				if (deviceObj is JObject obj) {
					jObj = obj;
				} else {
					var json = JsonConvert.SerializeObject(deviceObj);
					jObj = JObject.Parse(json);
				}

				var deviceId = jObj["Id"]?.ToString();
				if (string.IsNullOrEmpty(deviceId)) {
					result.SkippedDevices++;
					result.Warnings.Add("Skipped device with no Id.");
					continue;
				}

				RestoreSecrets(jObj, deviceId, bundle.EncryptedSecrets);

				var typeToken = jObj["_type"]?.ToString();
				if (!string.IsNullOrEmpty(typeToken)) {
					var typeType = Type.GetType(typeToken);
					if (typeType != null) {
						var device = jObj.ToObject(typeType);
						if (device is IColorTargetData ctd) {
							DataUtil.AddDeviceAsync(ctd, true).ConfigureAwait(false).GetAwaiter().GetResult();
							result.MigratedDevices++;
							continue;
						}
					}
				}

				var dynamicDevice = jObj.ToObject<dynamic>();
				if (dynamicDevice != null) {
					DataUtil.AddDeviceAsync(dynamicDevice, true).ConfigureAwait(false).GetAwaiter().GetResult();
					result.MigratedDevices++;
				} else {
					result.SkippedDevices++;
					result.Warnings.Add($"Could not deserialize device {deviceId}.");
				}
			} catch (Exception e) {
				result.SkippedDevices++;
				result.Warnings.Add($"Error importing device: {e.Message}");
			}
		}

		if (bundle.RoomMap != null) {
			try {
				DataUtil.SetObject<EmitterGraph>(bundle.RoomMap);
				result.Warnings.Add("Room map imported successfully.");
			} catch (Exception e) {
				result.Warnings.Add($"Failed to import room map: {e.Message}");
			}
		}

		if (bundle.SourceSettings.TryGetValue("SystemData", out var sdObj) && sdObj != null) {
			try {
				var importedSd = sdObj is JObject sdJObj
					? sdJObj.ToObject<SystemData>()
					: JsonConvert.DeserializeObject<SystemData>(JsonConvert.SerializeObject(sdObj));

				if (importedSd != null) {
					var currentSd = DataUtil.GetSystemData();
					importedSd.CheckDeviceVariables();
					if (string.IsNullOrEmpty(importedSd.DeviceId)) {
						importedSd.DeviceId = currentSd.DeviceId;
					}

					if (string.IsNullOrEmpty(importedSd.DeviceName)) {
						importedSd.DeviceName = currentSd.DeviceName;
					}

					DataUtil.SetSystemData(importedSd);
				}
			} catch (Exception e) {
				result.Warnings.Add($"Could not import system data: {e.Message}");
			}
		}

		result.Success = result.Errors.Count == 0;
		return result;
	}

	public string ExportToFile() {
		var bundle = CreateExport();
		var json = JsonConvert.SerializeObject(bundle, Formatting.Indented);
		var userDir = SystemUtil.GetUserDir();
		var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
		var outPath = Path.Combine(userDir, $"glimmr_export_{stamp}.json");
		File.WriteAllText(outPath, json, Encoding.UTF8);
		return outPath;
	}

	public ExportBundle ImportFromFile(string path) {
		if (!File.Exists(path)) {
			throw new FileNotFoundException("Export file not found.", path);
		}

		var json = File.ReadAllText(path, Encoding.UTF8);
		var bundle = JsonConvert.DeserializeObject<ExportBundle>(json);
		if (bundle == null) {
			throw new InvalidOperationException("Failed to deserialize export bundle.");
		}

		return bundle;
	}

	public byte[] ExportToZip() {
		var bundle = CreateExport();
		var json = JsonConvert.SerializeObject(bundle, Formatting.Indented);

		using var memoryStream = new MemoryStream();
		using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true)) {
			var entry = archive.CreateEntry("glimmr_export.json", CompressionLevel.Optimal);
			using (var entryStream = entry.Open()) {
				var bytes = Encoding.UTF8.GetBytes(json);
				entryStream.Write(bytes, 0, bytes.Length);
			}

			var userDir = SystemUtil.GetUserDir();
			var calibrationDir = Path.Combine(userDir, "calibration");
			if (Directory.Exists(calibrationDir)) {
				foreach (var file in Directory.GetFiles(calibrationDir, "*.png")
					         .Concat(Directory.GetFiles(calibrationDir, "*.jpg"))) {
					var fileName = Path.GetFileName(file);
					var imgEntry = archive.CreateEntry($"calibration/{fileName}", CompressionLevel.Optimal);
					using var imgEntryStream = imgEntry.Open();
					using var fileStream = File.OpenRead(file);
					fileStream.CopyTo(imgEntryStream);
				}
			}
		}

		return memoryStream.ToArray();
	}

	public SupportPackage GenerateSupportPackage() {
		var sd = DataUtil.GetSystemData();
		var devices = DataUtil.GetDevices();
		var stats = _controlService.Stats ?? StatUtil.GetStats().Result;

		var jl1 = new JsonLoader("ambientScenes");
		var jl2 = new JsonLoader("audioScenes");
		var ambientScenes = jl1.LoadDynamic<AmbientScene>();
		var audioScenes = jl2.LoadDynamic<AudioScene>();

		var enabledCount = 0;
		var capabilities = new List<AnonymizedDeviceCapability>();
		foreach (var device in devices) {
			try {
				IColorTargetData ctd = device;
				capabilities.Add(new AnonymizedDeviceCapability {
					Tag = ctd.Tag,
					Enabled = ctd.Enable,
					PropertyCount = ctd.KeyProperties?.Length ?? 0
				});
				if (ctd.Enable) enabledCount++;
			} catch {
				// Ignored
			}
		}

		var roomMap = DataUtil.GetObject<EmitterGraph>("EmitterGraph");

		var package = new SupportPackage {
			GeneratedAt = DateTime.UtcNow,
			GlimmrVersion = sd.Version,
			ConfigSummary = new ConfigSummary {
				DeviceCount = devices.Count,
				EnabledDeviceCount = enabledCount,
				AmbientSceneCount = ambientScenes.Count,
				AudioSceneCount = audioScenes.Count,
				DeviceMode = sd.DeviceMode.ToString(),
				LedCount = sd.LedCount,
				CaptureMode = sd.CaptureMode.ToString(),
				HasRoomMap = roomMap != null
			},
			ProviderVersions = GetProviderVersions(),
			DiscoveryLogs = ReadRecentLogLines(100),
			NetworkDiagnostics = GetNetworkDiagnostics(),
			AnonymizedDeviceCapabilities = capabilities,
			PerformanceMetrics = new PerformanceMetrics {
				CpuUsage = stats.CpuUsage,
				CpuTemp = stats.CpuTemp,
				MemoryUsage = stats.MemoryUsage,
				Fps = stats.Fps.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
				Uptime = stats.Uptime
			}
		};

		return package;
	}

	private static string[] ExtractProviderIds(List<dynamic> devices) {
		var tags = new HashSet<string>();
		foreach (var device in devices) {
			try {
				IColorTargetData ctd = device;
				if (!string.IsNullOrEmpty(ctd.Tag)) {
					tags.Add(ctd.Tag);
				}
			} catch {
				// Ignored
			}
		}

		return tags.ToArray();
	}

	private static void RedactSecrets(JObject jObj, string deviceId, Dictionary<string, string> secrets) {
		foreach (var propName in SecretPropertyNames) {
			var token = jObj[propName];
			if (token == null || token.Type != JTokenType.String) continue;
			var value = token.Value<string>();
			if (string.IsNullOrEmpty(value)) continue;

			var key = $"{deviceId}:{propName}";
			secrets[key] = Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
			jObj[propName] = "[REDACTED]";
		}
	}

	private static void RestoreSecrets(JObject jObj, string deviceId, Dictionary<string, string> secrets) {
		foreach (var propName in SecretPropertyNames) {
			var key = $"{deviceId}:{propName}";
			if (!secrets.TryGetValue(key, out var encoded)) continue;
			try {
				var value = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
				jObj[propName] = value;
			} catch (Exception e) {
				Log.Warning($"Failed to restore secret {key}: {e.Message}");
			}
		}
	}

	private static Dictionary<string, string> GetProviderVersions() {
		var versions = new Dictionary<string, string>();
		try {
			var entry = Assembly.GetEntryAssembly();
			if (entry != null) {
				foreach (var refAsm in entry.GetReferencedAssemblies()) {
					if (refAsm.Name != null && (
						    refAsm.Name.Contains("Glimmr") ||
						    refAsm.Name.Contains("Nanoleaf") ||
						    refAsm.Name.Contains("Hue") ||
						    refAsm.Name.Contains("Lifx") ||
						    refAsm.Name.Contains("WLED") ||
						    refAsm.Name.Contains("DreamScreen") ||
						    refAsm.Name.Contains("OpenRGB") ||
						    refAsm.Name.Contains("Yeelight") ||
						    refAsm.Name.Contains("Adalight"))) {
						versions[refAsm.Name] = refAsm.Version?.ToString() ?? "unknown";
					}
				}
			}
		} catch (Exception e) {
			Log.Warning($"Error reading provider versions: {e.Message}");
		}

		return versions;
	}

	private static List<string> ReadRecentLogLines(int lineCount) {
		var lines = new List<string>();
		try {
			var dt = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
			string logPath;
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				var userPath = SystemUtil.GetUserDir();
				logPath = Path.Combine(userPath, "log", $"glimmr{dt}.log");
			} else {
				logPath = $"/var/log/glimmr/glimmr{dt}.log";
			}

			if (!File.Exists(logPath)) return lines;

			using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			using var reader = new StreamReader(stream);
			var allLines = new List<string>();
			while (reader.ReadLine() is { } line) {
				allLines.Add(line);
			}

			var start = Math.Max(0, allLines.Count - lineCount);
			lines.AddRange(allLines.GetRange(start, allLines.Count - start));
		} catch (Exception e) {
			lines.Add($"[Error reading logs: {e.Message}]");
		}

		return lines;
	}

	private static NetworkDiagnostics GetNetworkDiagnostics() {
		var diag = new NetworkDiagnostics {
			LocalIpAddress = IpUtil.GetLocalIpAddress()
		};

		try {
			var interfaces = NetworkInterface.GetAllNetworkInterfaces();
			foreach (var nic in interfaces) {
				if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

				var ipProps = nic.GetIPProperties();
				var ipv4 = ipProps.UnicastAddresses
					.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);

				diag.Interfaces.Add(new NetworkInterfaceInfo {
					Name = nic.Name,
					Type = nic.NetworkInterfaceType.ToString(),
					Status = nic.OperationalStatus.ToString(),
					IpAddress = ipv4?.Address.ToString() ?? "",
					SupportsMulticast = nic.SupportsMulticast
				});

				if (nic.OperationalStatus == OperationalStatus.Up && nic.SupportsMulticast) {
					diag.MulticastAvailable = true;
				}
			}
		} catch (Exception e) {
			Log.Warning($"Error getting network diagnostics: {e.Message}");
		}

		return diag;
	}
}
