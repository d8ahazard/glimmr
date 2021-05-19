using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Glimmr.Models.ColorTarget {
	public interface IColorTargetData {
		public bool Enable { get; set; }
		public int Brightness { get; set; }
		public int FrameDelay { get; set; }

		[JsonProperty] public SettingsProperty[] KeyProperties { get; set; }

		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string LastSeen { get; set; }
		public string Name { get; set; }
		public string Tag { get; set; }
		public void UpdateFromDiscovered(IColorTargetData data);
	}

	[Serializable]
	public class SettingsProperty {
		[JsonProperty] public string ValueMax { get; set; }
		[JsonProperty] public string ValueMin { get; set; }
		[JsonProperty] public string ValueStep { get; set; }
		[JsonProperty] public Dictionary<string, string> Options;
		[JsonProperty] public string ValueLabel;
		[JsonProperty] public string ValueName;
		[JsonProperty] public string ValueType;

		public SettingsProperty() {
		}

		public SettingsProperty(string name, string type, string label, Dictionary<string, string> options = null) {
			ValueName = name;
			ValueType = type;
			ValueLabel = label;
			Options = options ?? new Dictionary<string, string>();
		}
	}
}