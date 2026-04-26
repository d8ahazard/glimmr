#region

using System;
using System.ComponentModel;
using System.Globalization;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget.RazerChroma;

[Serializable]
public class RazerChromaData : IColorTargetData {
	[JsonProperty]
	public int Brightness { get; set; } = 100;

	[JsonProperty]
	public string SessionUrl { get; set; } = "";

	[DefaultValue(-1)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int TargetSector { get; set; } = -1;

	public RazerChromaData() {
		Tag = "RazerChroma";
		Name ??= Tag;
		Id = "";
		IpAddress = "127.0.0.1";
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
	}

	public RazerChromaData(string id, string name, string sessionUrl) {
		Id = id;
		Tag = "RazerChroma";
		Name = name;
		IpAddress = "127.0.0.1";
		SessionUrl = sessionUrl;
		Brightness = 100;
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
		var input = (RazerChromaData)data;
		IpAddress = input.IpAddress;
		Name = input.Name;
		SessionUrl = input.SessionUrl;
	}

	public SettingsProperty[] KeyProperties {
		get => new[] {
			new SettingsProperty("TargetSector", "sectormap", "Target Sector"),
			new SettingsProperty("Brightness", "number", "Brightness") {
				ValueMax = "100", ValueMin = "0", ValueStep = "1"
			}
		};
		set { }
	}
}
