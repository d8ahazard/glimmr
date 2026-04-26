#region

using System;
using System.ComponentModel;
using System.Globalization;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget.Hyperion;

[Serializable]
public class HyperionData : IColorTargetData {
	[DefaultValue(19444)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int Port { get; set; } = 19444;

	[DefaultValue(150)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int Priority { get; set; } = 150;

	[DefaultValue(1)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int LedCount { get; set; } = 1;

	public HyperionData() {
		Tag = "Hyperion";
		Name ??= Tag;
		Id = "";
		IpAddress = "";
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
	}

	[DefaultValue("Hyperion")]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Tag { get; set; } = "Hyperion";

	[DefaultValue("")]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Name { get; set; } = "";

	[DefaultValue("")]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Id { get; set; } = "";

	[DefaultValue("")]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public string IpAddress { get; set; } = "";

	[JsonProperty]
	public bool Enable { get; set; }

	[JsonProperty]
	public string LastSeen { get; set; }

	public void UpdateFromDiscovered(IColorTargetData data) {
		var input = (HyperionData)data;
		IpAddress = input.IpAddress;
		Name = input.Name;
		Port = input.Port;
		LedCount = input.LedCount;
	}

	public SettingsProperty[] KeyProperties { get; set; } = {
		new("Port", "number", "JSON-RPC Port") { ValueMin = "1", ValueMax = "65535" },
		new("Priority", "number", "Priority") { ValueMin = "1", ValueMax = "255" },
		new("LedCount", "number", "LED Count") { ValueMin = "1", ValueMax = "1000" }
	};
}
