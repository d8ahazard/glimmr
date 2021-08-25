#region

using System;
using System.ComponentModel;
using System.Globalization;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget.Adalight {
	[Serializable]
	public class AdalightData : IColorTargetData {
		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool ReverseStrip { get; set; }

		[DefaultValue(100)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int Brightness { get; set; }

		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int LedCount { get; set; }

		[JsonProperty] public int LedMultiplier { get; set; } = 1;

		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int Offset { get; set; }

		[DefaultValue(1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int Port { get; set; }

		[DefaultValue(115200)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int Speed { get; set; }

		[JsonProperty] public string Tag { get; set; } = "Adalight";


		public AdalightData() {
			Port = 3;
			Name = $"Adalight - COM{Port}";
			Id = Name;
			Brightness = 100;
			LedCount = 0;
			Speed = 115200;
			IpAddress = "localhost";
			LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
		}

		public AdalightData(int port, int ledCount) {
			Port = port;
			Name = $"Adalight - COM{port}";
			Id = Name;
			Brightness = 100;
			LedCount = ledCount;
			Speed = 115200;
			IpAddress = "localhost";
			LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
		}

		[JsonProperty] public string Id { get; set; }

		[JsonProperty] public string IpAddress { get; set; }

		[JsonProperty] public string LastSeen { get; set; }

		[JsonProperty] public string Name { get; set; }

		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool Enable { get; set; }

		public SettingsProperty[] KeyProperties { get; set; } = {
			new("ledmap", "ledmap", ""),
			new("Offset", "text", "Led Offset"),
			new("LedCount", "text", "Led Count"),
			new("LedMultiplier", "number", "LED Multiplier") {
				ValueMin = "-5", ValueStep = "1", ValueMax="5", ValueHint = "Positive values to multiply (skip), negative values to divide (duplicate)."
			},
			new("Speed", "text", "Connection Speed (Baud Rate)"),
			new("ReverseStrip", "check", "Reverse Strip"){ValueHint = "Reverse the order of the leds to clockwise (facing screen)."}
		};


		public void UpdateFromDiscovered(IColorTargetData data) {
		}
	}
}