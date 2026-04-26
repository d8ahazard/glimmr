#region

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget.Dirigera;

[Serializable]
public class DirigeraData : IColorTargetData {
	[JsonProperty]
	public int Brightness { get; set; } = 100;

	[DefaultValue(-1)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int TargetSector { get; set; }

	[JsonProperty]
	public string HubIp { get; set; } = "";

	[JsonProperty]
	public string AccessToken { get; set; } = "";

	[JsonProperty]
	public string DeviceId { get; set; } = "";

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

	public DirigeraData() {
		Tag = "Dirigera";
		Name ??= Tag;
		Id = "";
		IpAddress = "";
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
	}

	public DirigeraData(string id, string hubIp, string accessToken, string deviceId, string name) {
		Tag = "Dirigera";
		Id = id;
		HubIp = hubIp;
		IpAddress = hubIp;
		AccessToken = accessToken;
		DeviceId = deviceId;
		Name = string.IsNullOrEmpty(name) ? "Dirigera " + deviceId : name;
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
	}

	public void UpdateFromDiscovered(IColorTargetData data) {
		var input = (DirigeraData)data;
		if (input == null) {
			throw new ArgumentNullException(nameof(data));
		}

		IpAddress = input.IpAddress;
		HubIp = input.HubIp;
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
			new SettingsProperty("HubIp", "text", "Dirigera Hub IP") {
				ValueHint = "IP address of your IKEA Dirigera hub."
			},
			new SettingsProperty("AccessToken", "text", "Access Token") {
				ValueHint = "Bearer token for hub API access."
			},
			new SettingsProperty("DeviceId", "text", "Device ID") {
				ValueHint = "The target light device ID on the hub."
			}
		};
	}
}
