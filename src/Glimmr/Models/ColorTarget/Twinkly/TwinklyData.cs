#region

using System;
using System.ComponentModel;
using System.Globalization;
using Glimmr.Models.Util;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget.Twinkly;

[Serializable]
public class TwinklyData : IColorTargetData {
	[JsonProperty]
	public int Brightness { get; set; } = 100;

	[JsonProperty]
	public int LedCount { get; set; }

	[DefaultValue(-1)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int TargetSector { get; set; }

	/// <summary>
	///     Authentication token obtained from the device.
	/// </summary>
	[JsonProperty]
	public string Token { get; set; } = "";

	public TwinklyData() {
		Tag = "Twinkly";
		Name ??= Tag;
		Id = "";
		IpAddress = "";
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
	}

	public TwinklyData(string id, string ip, string name, int ledCount) {
		Id = id;
		Tag = "Twinkly";
		Name = name;
		IpAddress = ip;
		LedCount = ledCount;
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
		var input = (TwinklyData)data;
		IpAddress = input.IpAddress;
		Name = input.Name;
		LedCount = input.LedCount;
	}

	public SettingsProperty[] KeyProperties { get; set; } = {
		new("TargetSector", "sectormap", "Target Sector"),
		new("Brightness", "number", "Brightness") { ValueMax = "100", ValueMin = "0" }
	};
}
