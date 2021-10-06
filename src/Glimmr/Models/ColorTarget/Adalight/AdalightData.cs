#region

using System;
using System.ComponentModel;
using System.Globalization;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget.Adalight {
	[Serializable]
	public class AdalightData : IColorTargetData {
		/// <summary>
		/// Reverse strip direction.
		/// </summary>
		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool ReverseStrip { get; set; }

		/// <summary>
		/// Device brightness.
		/// </summary>
		[DefaultValue(100)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int Brightness { get; set; }

		/// <summary>
		/// Number of LEDs on device. Must match Adalight settings, or device won't respond.
		/// </summary>
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int LedCount { get; set; }

		/// <summary>
		/// Scale factor for LED counts related to master grid.
		/// </summary>
		[JsonProperty] public float LedMultiplier { get; set; } = 1.0f;

		
		/// <summary>
		/// Offset of leds from lower-right corner of master grid.
		/// </summary>
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int Offset { get; set; }
		
		/// <summary>
		/// Baud rate for device.
		/// </summary>

		[DefaultValue(115200)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int Speed { get; set; }
		
		/// <summary>
		/// Port for device communication.
		/// </summary>

		[DefaultValue("COM1")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Port { get; set; }

		/// <summary>
		/// Gamma adjustment factor. You probably don't want to go higher than 3 or so.
		/// </summary>
		[DefaultValue(2.2)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public float GammaFactor { get; set; } = 2.2f;
		
		
		/// <summary>
		/// Device tag.
		/// </summary>
		[JsonProperty] public string Tag { get; set; } = "Adalight";


		public AdalightData() {
			Port = "COM1";
			Name = $"Adalight - {Port}";
			Id = Name;
			Brightness = 100;
			LedCount = 0;
			Speed = 115200;
			IpAddress = "localhost";
			LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
		}

		public AdalightData(string port, int ledCount) {
			Port = port;
			Name = $"Adalight - {port}";
			Id = Name;
			Brightness = 100;
			LedCount = ledCount;
			Speed = 115200;
			IpAddress = "localhost";
			LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Device ID.
		/// </summary>
		[JsonProperty] public string Id { get; set; }

		/// <summary>
		/// Unused for adalight.
		/// </summary>
		[JsonProperty] public string IpAddress { get; set; }

		/// <summary>
		/// Last time the device was seen during discovery.
		/// </summary>
		[JsonProperty] public string LastSeen { get; set; }

		/// <summary>
		/// Device name.
		/// </summary>
		[JsonProperty] public string Name { get; set; }
		
		/// <summary>
		/// Enable streaming.
		/// </summary>

		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool Enable { get; set; }
		
		/// <summary>
		/// UI Properties.
		/// </summary>

		public SettingsProperty[] KeyProperties { get; set; } = {
			new("ledmap", "ledmap", ""),
			new("Offset", "text", "Led Offset"),
			new("LedCount", "text", "Led Count"),
			new("LedMultiplier", "ledMultiplier", ""),
			new("GammaFactor", "number", "Gamma Correction")
				{ValueMin = "1.0",ValueMax = "5", ValueStep = ".1", ValueHint = "1 = No adjustment, 2.2 = Recommended"},
			new("Speed", "text", "Connection Speed (Baud Rate)"),
			new("ReverseStrip", "check", "Reverse Strip")
				{ValueHint = "Reverse the order of the leds to clockwise (facing screen)."}
		};


		public void UpdateFromDiscovered(IColorTargetData data) {
		}
	}
}