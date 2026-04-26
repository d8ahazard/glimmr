#region

using System;
using System.ComponentModel;
using System.Globalization;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget.HomeAssistant;

[Serializable]
public class HomeAssistantData : IColorTargetData {
	[JsonProperty]
	public string HaUrl { get; set; } = "";

	[JsonProperty]
	public string AccessToken { get; set; } = "";

	[JsonProperty]
	public string EntityId { get; set; } = "";

	[DefaultValue(100)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int Brightness { get; set; } = 100;

	[DefaultValue(-1)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int TargetSector { get; set; } = -1;

	public HomeAssistantData() {
		Tag = "HomeAssistant";
		Name ??= Tag;
		Id = "";
		IpAddress = "";
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
	}

	[DefaultValue("HomeAssistant")]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Tag { get; set; } = "HomeAssistant";

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
		var input = (HomeAssistantData)data;
		IpAddress = input.IpAddress;
		Name = input.Name;
		EntityId = input.EntityId;
	}

	public SettingsProperty[] KeyProperties { get; set; } = {
		new("HaUrl", "text", "Home Assistant URL") { ValueHint = "e.g. homeassistant.local:8123" },
		new("AccessToken", "text", "Long-Lived Access Token"),
		new("EntityId", "text", "Entity ID") { ValueHint = "e.g. light.living_room" },
		new("Brightness", "number", "Brightness") { ValueMax = "100", ValueMin = "0" },
		new("TargetSector", "sectormap", "Target Sector")
	};
}
