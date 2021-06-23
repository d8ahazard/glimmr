using System;
using System.ComponentModel;
using System.Globalization;
using Glimmr.Models.Util;
using Newtonsoft.Json;

namespace Glimmr.Models.ColorTarget.Yeelight {
	public class YeelightData : IColorTargetData {
		[DefaultValue(-1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int TargetSector { get; set; }

		[DefaultValue("")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]

		public string Name { get; set; } = "";

		[DefaultValue("")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]

		public string Id { get; set; } = "";

		[DefaultValue("Yeelight")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]

		public string Tag { get; set; } = "Yeelight";

		[DefaultValue("")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]

		public string IpAddress { get; set; } = "";

		[DefaultValue(255)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]

		public int Brightness { get; set; } = 255;
		
		public bool Enable { get; set; }
		public string LastSeen { get; set; }


		public YeelightData() {
			LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
			if (!string.IsNullOrEmpty(Id)) Name = StringUtil.UppercaseFirst(Id);
		}

		public YeelightData(string id) {
			Id = id;
			LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
			if (!string.IsNullOrEmpty(Id)) Name = StringUtil.UppercaseFirst(Id);
		}

		
		public void UpdateFromDiscovered(IColorTargetData existing) {
			Name = existing.Name;
			IpAddress = existing.IpAddress;
		}

		public SettingsProperty[] KeyProperties { get; set; } = {
			new("TargetSector", "sectormap", "Target Sector")
		};
	}
}