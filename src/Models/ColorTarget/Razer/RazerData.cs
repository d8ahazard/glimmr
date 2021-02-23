using System.ComponentModel;
using Newtonsoft.Json;

namespace Glimmr.Models.ColorTarget.Razer {
	public class RazerData : StreamingData {
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
	}
}