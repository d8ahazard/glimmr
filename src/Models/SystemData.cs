using System.ComponentModel;
using System.Drawing;
using Newtonsoft.Json;

namespace Glimmr.Models {
	public class SystemData {
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int DeviceMode { get; set; }
		
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int DeviceGroup { get; set; }
		
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int AmbientMode { get; set; }

		[JsonProperty] public string AmbientColor { get; set; } = "FFFFFF";
		
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int AmbientShow { get; set; }
		
		[DefaultValue("")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string GroupName { get; set; }
		
		[DefaultValue("US/Central")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string TimeZone { get; set; } = "US/Central";
		
		[DefaultValue("main")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string UpdateBranch { get; set; } = "main";
		
		[DefaultValue("dark")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Theme { get; set; } = "dark";
		
		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool AutoUpdate { get; set; }
		
		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool DefaultSet { get; set; }
		
		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool ShowSource { get; set; }
		
		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool AutoDisabled { get; set; }

		[DefaultValue(true)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool EnableAutoDisable { get; set; } = true;
		
		[DefaultValue(.01f)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public float AudioThreshold { get; set; } = .01f;
		
		[DefaultValue(1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int Sensitivity { get; set; }
		
		[DefaultValue(1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int CamType { get; set; } = 1;
		
		[DefaultValue(2)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int CaptureMode { get; set; } = 2;
		
		[DefaultValue(255)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int MinBrightness { get; set; } = 255;
		
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int SaturationBoost { get; set; }
		
		[DefaultValue(1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int RecId { get; set; } = 1;
		
		[DefaultValue("Dreamscreen4K")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string DevType { get; set; } = "Dreamscreen4K";
		
		[DefaultValue("")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string DsIp { get; set; }
		
		[DefaultValue("")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string RecDev { get; set; }
		
		//TODO: Make getter for this always retrieve same value used by setup script
		[DefaultValue("")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Serial { get; set; }

	}
}