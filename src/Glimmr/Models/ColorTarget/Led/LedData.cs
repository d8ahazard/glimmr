#region

using System;
using System.ComponentModel;
using System.Globalization;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget.Led {
	[Serializable]
	public class LedData : IColorTargetData {
		/// <summary>
		/// Enable gamma correction.
		/// </summary>
		[DefaultValue(true)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool FixGamma { get; set; } = true;

		/// <summary>
		/// Reverse the order of data sent to leds.
		/// </summary>
		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool ReverseStrip { get; set; }

		/// <summary>
		/// Device brightness.
		/// </summary>
		[JsonProperty] public int Brightness { get; set; }

		/// <summary>
		/// GPIO Number to use for device. Don't change this.
		/// </summary>
		[DefaultValue(18)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int GpioNumber { get; set; } = 18;

		/// <summary>
		/// Number of LEDs in strip.
		/// </summary>
		[DefaultValue(300)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int LedCount { get; set; } = 300;

		/// <summary>
		/// Scale factor for LED counts related to master grid.
		/// </summary>
		[JsonProperty] public float LedMultiplier { get; set; } = 1.0f;

		/// <summary>
		/// Per-led milliamp usage. Default is 30. 
		/// </summary>
		[DefaultValue(30)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int MilliampsPerLed { get; set; } = 30;

		/// <summary>
		/// Offset of leds from lower-right corner of master grid.
		/// </summary>
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int Offset { get; set; }
		
		/// <summary>
		/// LED Strip Type.
		/// 0 = WS2812,
		/// 1 = SK6812W (RGBW),
		/// 2 = WS2811,
		/// Default = WS2812
		/// </summary>
		[JsonProperty] public int StripType { get; set; }

		/// <summary>
		/// Device tag.
		/// </summary>
		[DefaultValue("Led")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Tag { get; set; } = "Led";

		public LedData() {
			LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
		}

		[JsonProperty] public string Name {
			get => $"LED {Id} - GPIO {GpioNumber}";
			set {
			}
		}

		/// <summary>
		/// Unique device identifier.
		/// </summary>
		public string Id { get; set; } = "";
		
		/// <summary>
		/// Device IP Address.
		/// </summary>
		[JsonProperty] public string IpAddress { get; set; } = "";

		/// <summary>
		/// Enable streaming.
		/// </summary>
		[JsonProperty] public bool Enable { get; set; }

		/// <summary>
		/// Last time the device was seen during discovery.
		/// </summary>
		[JsonProperty] public string LastSeen { get; set; }


		public void UpdateFromDiscovered(IColorTargetData data) {
		}

		/// <summary>
		/// UI Elements.
		/// </summary>
		[JsonProperty]
		public SettingsProperty[] KeyProperties { get; set; } = {
			new("ledmap", "ledmap", ""),
			new("LedCount", "text", "Led Count"),
			new("Offset", "text", "Led Offset"),
			new("LedMultiplier", "ledMultiplier", ""),
			new("ReverseStrip", "check", "Reverse Strip")
				{ValueHint = "Reverse the order of the leds to clockwise (facing screen)."},
			new("FixGamma", "check", "Fix Gamma") {ValueHint = "Automatically correct Gamma (recommended)"},
			new("MilliampsPerLed", "text", "Milliamps Per LED")
				{ValueHint = "'Default' = 30 (.3w), 'Normal' = 55 (.55w)"}
		};
	}
}