using System.ComponentModel;
using Newtonsoft.Json;

namespace Glimmr.Models.ColorTarget.Yeelight {
	public class YeelightData : StreamingData {
		[DefaultValue(-1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int TargetSector { get; set; }
		
		
		public void CopyExisting(YeelightData existing) {
			TargetSector = existing.TargetSector;
			Brightness = existing.Brightness;
			Enable = existing.Enable;
		}
	}
}