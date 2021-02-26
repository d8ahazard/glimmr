using System;
using System.ComponentModel;
using Newtonsoft.Json;

namespace Glimmr.Models.ColorTarget.Razer {
	public class RazerData : IColorTargetData {
		[DefaultValue(100)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int KeyboardOffset { get; set; }
		
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int MouseOffset { get; set; }
		
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int MousePadOffset { get; set; }
		
		[DefaultValue(20)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int KeypadOffset { get; set; }
		
		[DefaultValue(50)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int HeadsetOffset { get; set; }

		public string Name { get; set; }
		public string Id { get; set; }
		public string Tag { get; set; }
		public string IpAddress { get; set; }
		public int Brightness { get; set; }
		public bool Enable { get; set; }
		public string LastSeen { get; set; }
		public string DeviceTag { get; set; } = "Unknown";

		public void CopyExisting(IColorTargetData existing) {
			var rd = (RazerData) existing;
			DeviceTag = rd.DeviceTag;
		}
	}
}