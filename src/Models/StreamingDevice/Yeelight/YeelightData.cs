using System.ComponentModel;
using Newtonsoft.Json;

namespace Glimmr.Models.StreamingDevice.Yeelight {
	public class YeelightData : StreamingData {
		[DefaultValue(-1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int TargetSector { get; set; }
		
		[DefaultValue(255)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int Brightness { get; set; }
	}
}