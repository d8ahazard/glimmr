#region

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget.Tuya;

[Serializable]
public class TuyaData : IColorTargetData {
	[JsonProperty]
	public int Brightness { get; set; } = 100;

	[DefaultValue(-1)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int TargetSector { get; set; }

	[JsonProperty]
	public string DeviceId { get; set; } = "";

	[JsonProperty]
	public string LocalKey { get; set; } = "";

	[JsonProperty]
	public string Tag { get; set; }

	[JsonProperty]
	public string Name { get; set; }

	[JsonProperty]
	public string Id { get; set; }

	[JsonProperty]
	public string IpAddress { get; set; }

	[JsonProperty]
	public bool Enable { get; set; }

	[JsonProperty]
	public string LastSeen { get; set; }

	public TuyaData() {
		Tag = "Tuya";
		Name ??= Tag;
		Id = "";
		IpAddress = "";
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
	}

	public TuyaData(string id, string ip, string deviceId, string localKey, string name) {
		Tag = "Tuya";
		Id = id;
		IpAddress = ip;
		DeviceId = deviceId;
		LocalKey = localKey;
		Name = string.IsNullOrEmpty(name) ? "Tuya " + deviceId : name;
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
	}

	public void UpdateFromDiscovered(IColorTargetData data) {
		var input = (TuyaData)data;
		if (input == null) {
			throw new ArgumentNullException(nameof(data));
		}

		IpAddress = input.IpAddress;
		Name = input.Name;
		DeviceId = input.DeviceId;
	}

	public SettingsProperty[] KeyProperties {
		get => Kps();
		set { }
	}

	private SettingsProperty[] Kps() {
		return new[] {
			new SettingsProperty("sectormap", "sectormap", ""),
			new SettingsProperty("Brightness", "number", "Brightness") { ValueMin = "1", ValueMax = "100" },
			new SettingsProperty("DeviceId", "text", "Tuya Device ID") {
				ValueHint = "The device ID from your Tuya developer account."
			},
			new SettingsProperty("LocalKey", "text", "Local Key") {
				ValueHint = "The local key from your Tuya developer account."
			}
		};
	}
}
