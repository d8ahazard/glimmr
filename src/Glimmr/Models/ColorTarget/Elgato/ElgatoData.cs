#region

using System;
using System.ComponentModel;
using System.Globalization;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget.Elgato;

[Serializable]
public class ElgatoData : IColorTargetData {
	[JsonProperty]
	public int Brightness { get; set; } = 50;

	/// <summary>
	///     Color temperature in mireds (143-344 range = 7000K-2900K).
	/// </summary>
	[DefaultValue(200)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int Temperature { get; set; } = 200;

	[DefaultValue(-1)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int TargetSector { get; set; }

	public ElgatoData() {
		Tag = "Elgato";
		Name ??= Tag;
		Id = "";
		IpAddress = "";
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
	}

	public ElgatoData(string id, string ip, string name) {
		Id = id;
		Tag = "Elgato";
		Name = name;
		IpAddress = ip;
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
	}

	[JsonProperty]
	public string Tag { get; set; }

	[JsonProperty]
	public string Name { get; set; } = "";

	[JsonProperty]
	public string Id { get; set; }

	[JsonProperty]
	public string IpAddress { get; set; }

	[JsonProperty]
	public bool Enable { get; set; }

	[JsonProperty]
	public string LastSeen { get; set; }

	public void UpdateFromDiscovered(IColorTargetData data) {
		var input = (ElgatoData)data;
		IpAddress = input.IpAddress;
		Name = input.Name;
	}

	public SettingsProperty[] KeyProperties { get; set; } = {
		new("TargetSector", "sectormap", "Target Sector"),
		new("Brightness", "number", "Brightness") { ValueMax = "100", ValueMin = "0" },
		new("Temperature", "number", "Color Temperature (mireds)") {
			ValueMin = "143", ValueMax = "344", ValueStep = "1",
			ValueHint = "143 = 7000K (cool), 344 = 2900K (warm)"
		}
	};
}
