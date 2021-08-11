#region

using System;
using System.ComponentModel;
using System.Globalization;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget.Led {
	[Serializable]
	public class LedData : IColorTargetData {

		[DefaultValue(true)] [JsonProperty] public bool AutoBrightnessLevel { get; set; } = true;

		[DefaultValue(true)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool FixGamma { get; set; } = true;


		[DefaultValue(2000)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int AblMaxMilliamps { get; set; } = 5000;

		[DefaultValue(18)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int GpioNumber { get; set; } = 18;

		[DefaultValue(300)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int LedCount { get; set; } = 300;

		[DefaultValue(55)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int MilliampsPerLed { get; set; } = 25;

		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int Offset { get; set; }

		[JsonProperty] public int StartupAnimation { get; set; }
		[JsonProperty] public int StripType { get; set; }

		[JsonProperty]
		public string Name {
			get => $"LED {Id} - GPIO {GpioNumber}";
			set { }
		}

		public string Id { get; set; } = "";

		[DefaultValue("Led")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Tag { get; set; } = "Led";

		public string IpAddress { get; set; } = "";
		public int Brightness { get; set; }

		public bool Enable { get; set; }
		public string LastSeen { get; set; }

		public void UpdateFromDiscovered(IColorTargetData data) {
		}

		public LedData() {
			LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
		}

		[JsonProperty]
		public SettingsProperty[] KeyProperties { get; set; } = {
			new("ledmap", "ledmap", ""),
			new("Offset", "text", "Led Offset"),
			new("LedCount", "text", "Led Count"),
			new("FixGamma", "check", "Fix Gamma"),
			new("AutoBrightnessLevel", "check", "Enable Auto Brightness"),
			new("MilliampsPerLed", "text", "Milliamps per led"),
			new("AblMaxMilliamps", "text", "Total Max Milliamps")
		};
	}
}