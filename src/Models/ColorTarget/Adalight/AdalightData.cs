#region

using System;
using System.ComponentModel;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget.Adalight {
	[Serializable]
	public class AdalightData : IColorTargetData {
		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool ReverseStrip { get; set; }

		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int LedCount { get; set; }

		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int Offset { get; set; }

		[DefaultValue(1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int Port { get; set; }

		[DefaultValue(115200)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int Speed { get; set; }

		public AdalightData() {
		}

		public AdalightData(int port, int ledCount) {
			Port = port;
			Name = $"Adalight - COM{port}";
			Id = Name;
			Brightness = 100;
			LedCount = ledCount;
			Speed = 115200;
		}

		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool Enable { get; set; }

		[DefaultValue(100)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int Brightness { get; set; }

		public SettingsProperty[] KeyProperties { get; set; } = {
			new("ledmap", "ledmap", ""),
			new("Offset", "text", "Led Offset"),
			new("LedCount", "text", "Led Count"),
			new("Speed", "text", "Connection Speed (Baud Rate)"),
			new("ReverseStrip", "check", "Reverse Strip")
		};

		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string LastSeen { get; set; }
		public string Name { get; set; }
		public string Tag { get; set; } = "Adalight";

		public void UpdateFromDiscovered(IColorTargetData data) {
		}
	}
}