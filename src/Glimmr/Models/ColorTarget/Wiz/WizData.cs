#region

using System;
using System.ComponentModel;
using System.Globalization;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget.Wiz;

[Serializable]
public class WizData : IColorTargetData {
	[JsonProperty]
	public int Brightness { get; set; } = 100;

	[DefaultValue(-1)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int TargetSector { get; set; }

	public WizData() {
		Tag = "Wiz";
		Name ??= Tag;
		Id = "";
		IpAddress = "";
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
	}

	public WizData(string id, string ip, string name) {
		Id = id;
		Tag = "Wiz";
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
		var input = (WizData)data;
		IpAddress = input.IpAddress;
		Name = input.Name;
	}

	public SettingsProperty[] KeyProperties { get; set; } = {
		new("TargetSector", "sectormap", "Target Sector"),
		new("Brightness", "number", "Brightness") { ValueMax = "100", ValueMin = "0" }
	};
}
