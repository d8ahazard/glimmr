#region

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Glimmr.Models.Util;
using Newtonsoft.Json;
using OpenRGB.NET.Enums;
using OpenRGB.NET.Models;

#endregion

namespace Glimmr.Models.ColorTarget.OpenRgb {
	public class OpenRgbData : IColorTargetData {
		[DefaultValue(DeviceType.Ledstrip)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]

		public DeviceType Type { get; set; }

		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int ActiveModeIndex { get; set; }

		[DefaultValue(255)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]


		public int Brightness { get; set; }
		
		[DefaultValue(1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]


		public int LedMultiplier { get; set; }

		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]

		public int DeviceId { get; set; }

		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]

		public int LedCount { get; set; }

		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]

		public int Offset { get; set; }

		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]

		public int Rotation { get; set; }

		[DefaultValue("")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]

		public string Description { get; set; }

		[DefaultValue("")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Location { get; set; }

		[DefaultValue("")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Serial { get; set; }


		[DefaultValue("OpenRgb")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]

		public string Tag { get; set; }


		[DefaultValue("Unknown")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Vendor { get; set; }

		[DefaultValue("")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Version { get; set; }


		public OpenRgbData() {
			Tag = "OpenRgb";
			Name ??= Tag;
			Id = "";
			IpAddress = "";
			Description = "";
			Location = Serial = Vendor = Version = "";
			LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
			Name = StringUtil.UppercaseFirst(Id);
		}

		public OpenRgbData(Device dev, int index, string ip) {
			Id = "OpenRgb" + index;
			DeviceId = index;
			IpAddress = ip;
			Name = dev.Name;
			Vendor = dev.Vendor;
			Type = dev.Type;
			//Zones = dev.Zones.ToList();
			Description = dev.Description;
			Version = dev.Version;
			Serial = dev.Serial;
			Location = dev.Location;
			ActiveModeIndex = dev.ActiveModeIndex;
			LedCount = dev.Leds.Length;
			//Modes = dev.Modes.ToList();
			Tag = "OpenRgb";
			Brightness = 255;
			LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
		}

		[JsonProperty] public string Id { get; set; }

		[DefaultValue("")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Name { get; set; }

		[DefaultValue("127.0.0.1")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]

		public string IpAddress { get; set; }

		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]

		public bool Enable { get; set; }

		[JsonProperty] public string LastSeen { get; set; }


		public SettingsProperty[] KeyProperties { get; set; } = {
			new("ledmap", "ledmap", ""),
			new("Offset", "number", "LED Offset"),
			new("LedMultiplier", "number", "LED Multiplier") {
				ValueMin = "-5", ValueStep = "1", ValueMax="5", ValueHint = "Positive values to multiply (skip), negative values to divide (duplicate)."
			},
			new("Rotation", "select", "Rotation", new Dictionary<string, string> {
				["0"] = "Normal",
				["90"] = "90 Degrees",
				["180"] = "180 Degrees (Mirror)",
				["270"] = "270 Degrees"
			})
		};


		public void UpdateFromDiscovered(IColorTargetData data) {
			var dev = (OpenRgbData) data;
			Name = dev.Name;
			Vendor = dev.Vendor;
			Type = dev.Type;
			//Zones = dev.Zones.ToList();
			Description = dev.Description;
			Version = dev.Version;
			Serial = dev.Serial;
			Location = dev.Location;
			ActiveModeIndex = dev.ActiveModeIndex;
			LedCount = dev.LedCount;
		}
	}
}