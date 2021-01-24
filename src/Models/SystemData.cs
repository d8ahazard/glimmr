using System.ComponentModel;
using System.Drawing;
using Glimmr.Models.ColorSource.Audio;
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
		
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int PreviewMode { get; set; }
		
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
		
		[DefaultValue(.01f)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public float AudioThreshold { get; set; } = .01f;

		[DefaultValue(ColorSource.Audio.AudioMap.MapType.Bottom)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public AudioMap.MapType AudioMap { get; set; } = ColorSource.Audio.AudioMap.MapType.Bottom;
		
		[DefaultValue(.0f)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public float AudioRotationSensitivity { get; set; }
		
		[DefaultValue(.0f)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public float AudioRotationSpeed { get; set; }
		
		[DefaultValue(.0f)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public float AudioRotationLower { get; set; }

		[DefaultValue(1f)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public float AudioRotationUpper { get; set; } = 1f;
		
		[DefaultValue(.5f)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public float AudioGain { get; set; } = .5f;
		
		[DefaultValue(.025f)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public float AudioMin { get; set; } = .025f;

		
		//TODO: Make getter for this always retrieve same value used by setup script
		[DefaultValue("")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Serial { get; set; }

		// Values for general LED settings
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int LedCount => LeftCount + RightCount + TopCount + BottomCount;
		
		[DefaultValue(24)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int LeftCount { get; set;} = 24;
		
		[DefaultValue(24)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int RightCount { get; set; } = 24;
		
		[DefaultValue(40)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int TopCount { get; set; } = 40;
		
		[DefaultValue(40)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int BottomCount { get; set; } = 40;
		
		[DefaultValue(24)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		
		public int VCountDs { get; set; } = 24;
		
		[DefaultValue(40)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int HCountDs { get; set; } = 40;


	}
}