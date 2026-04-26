#region

using System;
using System.ComponentModel;
using System.Globalization;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget.DeConz;

[Serializable]
public class DeConzData : IColorTargetData {
	[JsonProperty]
	public int Brightness { get; set; } = 255;

	[DefaultValue(8080)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int Port { get; set; } = 8080;

	[JsonProperty]
	public string ApiKey { get; set; } = "";

	[JsonProperty]
	public string LightId { get; set; } = "";

	[DefaultValue(-1)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int TargetSector { get; set; } = -1;

	public DeConzData() {
		Tag = "DeConz";
		Name ??= Tag;
		Id = "";
		IpAddress = "";
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
	}

	public DeConzData(string id, string ip, int port, string apiKey, string lightId, string name) {
		Id = id;
		Tag = "DeConz";
		Name = name;
		IpAddress = ip;
		Port = port;
		ApiKey = apiKey;
		LightId = lightId;
		Brightness = 255;
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
	}

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

	public void UpdateFromDiscovered(IColorTargetData data) {
		var input = (DeConzData)data;
		IpAddress = input.IpAddress;
		Name = input.Name;
		Port = input.Port;
		LightId = input.LightId;
	}

	public SettingsProperty[] KeyProperties {
		get => new[] {
			new SettingsProperty("TargetSector", "sectormap", "Target Sector"),
			new SettingsProperty("Brightness", "number", "Brightness") {
				ValueMax = "255", ValueMin = "0", ValueStep = "1"
			},
			new SettingsProperty("ApiKey", "text", "API Key"),
			new SettingsProperty("Port", "number", "Port") {
				ValueMax = "65535", ValueMin = "1", ValueStep = "1"
			}
		};
		set { }
	}
}
