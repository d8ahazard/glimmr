#region

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Glimmr.Enums;
using Glimmr.Models.Util;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget.Govee;

[Serializable]
public class GoveeData : IColorTargetData {
	[DefaultValue(false)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool ReverseStrip { get; set; }

	[JsonProperty]
	public float LedMultiplier { get; set; } = 1.0f;

	[JsonProperty]
	public int Brightness { get; set; } = 100;

	[JsonProperty]
	public int LedCount { get; set; }

	[JsonProperty]
	public int Offset { get; set; }

	[DefaultValue(-1)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int TargetSector { get; set; }

	[JsonProperty]
	public int SegmentCount { get; set; }

	[JsonProperty]
	public StripMode StripMode { get; set; }

	/// <summary>
	///     Govee device SKU (model number).
	/// </summary>
	[JsonProperty]
	public string Sku { get; set; } = "";

	/// <summary>
	///     Govee API key for cloud control.
	/// </summary>
	[JsonProperty]
	public string GoveeApiKey { get; set; } = "";

	/// <summary>
	///     Whether this device supports segment color control.
	/// </summary>
	[DefaultValue(false)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool SupportsSegments { get; set; }

	/// <summary>
	///     Whether the device was discovered on the LAN.
	/// </summary>
	[DefaultValue(false)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool LanEnabled { get; set; }

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

	public GoveeData() {
		Tag = "Govee";
		Name ??= Tag;
		Id = "";
		IpAddress = "";
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
	}

	public GoveeData(string id, string ipAddress, string sku, string name) {
		Tag = "Govee";
		Id = id;
		IpAddress = ipAddress;
		Sku = sku;
		Name = string.IsNullOrEmpty(name) ? "Govee " + sku : name;
		LanEnabled = !string.IsNullOrEmpty(ipAddress);
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
	}

	public void UpdateFromDiscovered(IColorTargetData data) {
		var input = (GoveeData)data;
		if (input == null) {
			throw new ArgumentNullException(nameof(data));
		}

		IpAddress = input.IpAddress;
		Name = input.Name;
		Sku = input.Sku;
		LanEnabled = input.LanEnabled;
		if (input.LedCount > 0) {
			LedCount = input.LedCount;
		}

		if (input.SegmentCount > 0) {
			SegmentCount = input.SegmentCount;
		}

		SupportsSegments = input.SupportsSegments;
	}

	public SettingsProperty[] KeyProperties {
		get => Kps();
		set { }
	}

	private SettingsProperty[] Kps() {
		var props = new List<SettingsProperty> {
			new("StripMode", "select", "Strip Mode", new Dictionary<string, string> {
				["0"] = "Normal",
				["1"] = "Sectored",
				["3"] = "Single Color"
			}),
			new("Brightness", "number", "Brightness") { ValueMin = "1", ValueMax = "100" },
			new("LedCount", "number", "LED Count"),
			new("Offset", "number", "Strip Offset"),
			new("LedMultiplier", "ledMultiplier", ""),
			new("ReverseStrip", "check", "Reverse Strip Direction"),
			new("GoveeApiKey", "text", "Govee API Key") {
				ValueHint = "Required for cloud-based segment control. Get from Govee Developer portal."
			}
		};

		if (StripMode == StripMode.Single) {
			props.Insert(1, new SettingsProperty("sectormap", "sectormap", ""));
		} else {
			props.Insert(1, new SettingsProperty("ledmap", "ledmap", ""));
		}

		return props.ToArray();
	}
}
