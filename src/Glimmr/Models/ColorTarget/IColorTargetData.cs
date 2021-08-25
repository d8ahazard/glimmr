#region

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget {
	public interface IColorTargetData {
		public bool Enable { get; }


		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
		public SettingsProperty[] KeyProperties { get; set; }

		public string Id { get; }
		public string IpAddress { get; }
		public string LastSeen { get; }
		public string Name { get; }
		public void UpdateFromDiscovered(IColorTargetData data);
	}

	[Serializable]
	public class SettingsProperty {
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string ValueLabel { get; set; } = "";

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string ValueHint { get; set; } = "";

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string ValueMax { get; set; } = "100";

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string ValueMin { get; set; } = "0";

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string ValueName { get; set; } = "";

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string ValueStep { get; set; } = "1";

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string ValueType { get; set; } = "";

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
		public Dictionary<string, string> Options;

		public SettingsProperty() {
			Options = new Dictionary<string, string>();
		}

		public SettingsProperty(string name, string type, string label, Dictionary<string, string>? options = null) {
			ValueName = name;
			ValueType = type;
			ValueLabel = label;
			ValueMax = "100";
			ValueMin = "0";
			ValueStep = "1";
			ValueHint = "";
			Options = options ?? new Dictionary<string, string>();
		}
	}
}