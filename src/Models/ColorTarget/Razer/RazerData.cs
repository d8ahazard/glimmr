using System;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

namespace Glimmr.Models.ColorTarget.Razer {
	public class RazerData : IColorTargetData {
		
		[DefaultValue(50)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int Offset { get; set; }
		public bool Reverse { get; set; }

		public string Name { get; set; }
		public string Id { get; set; }
		public string Tag { get; set; }
		public string IpAddress { get; set; }
		public int Brightness { get; set; }
		public bool Enable { get; set; }
		public string LastSeen { get; set; }
		public string DeviceTag { get; set; } = "Unknown";

		public RazerData() {
			
		}
		public void CopyExisting(IColorTargetData existing) {
			var rd = (RazerData) existing;
			DeviceTag = rd.DeviceTag;
		}

		public SettingsProperty[] KeyProperties { get; set; } = {
			new("Offset", "text", "Device Offset"),
			new("Reverse", "check", "Reverse LED Colors")
		};
	}
}