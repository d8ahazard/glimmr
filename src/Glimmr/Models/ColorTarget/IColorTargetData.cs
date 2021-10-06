#region

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget {
	/// <summary>
	/// Base class for various color target data classes.
	/// All color target data MUST have these properties,
	/// but will almost certainly implement more.
	/// 
	/// Refer to actual device JSON from /devices
	/// for full device structures.
	/// </summary>
	
	public abstract class IColorTargetData {
		/// <summary>
		/// If set, Glimmr will attempt to control this device.
		/// </summary>
		public abstract bool Enable { get;  set;}
		
		/// <summary>
		/// Unique device tag used for serilization/deserilization
		/// </summary>
		public abstract string Tag { get; set; }

		/// <summary>
		/// An array of properties that will be auto-filled in the web UI.
		/// </summary>
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
		public abstract SettingsProperty[] KeyProperties { get; set; }

		/// <summary>
		/// A unique device identifier.
		/// </summary>
		public abstract string Id { get;  set;}
		
		/// <summary>
		/// The device IP address.
		/// </summary>
		public abstract string IpAddress { get; set; }
		
		/// <summary>
		/// The last time the device was seen via device discovery.
		/// </summary>
		public abstract string LastSeen { get;  set;}

		/// <summary>
		/// The device name.
		/// </summary>
		public abstract string Name { get;  set;}
		public abstract void UpdateFromDiscovered(IColorTargetData data);
	}

	/// <summary>
	/// A class used by the web UI to automagically generate device settings.
	/// </summary>
	[Serializable]
	public class SettingsProperty {
		/// <summary>
		/// If set, this will be shown beneath the property.
		/// </summary>
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string ValueHint { get; set; } = "";
		
		/// <summary>
		/// Main label for the setting.
		/// </summary>

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string ValueLabel { get; set; } = "";
		
		/// <summary>
		/// Maximum value that can be set for this property.
		/// </summary>

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string ValueMax { get; set; } = "100";
		
		/// <summary>
		/// Minimum Value that can be set for this property.
		/// </summary>

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string ValueMin { get; set; } = "0";
		
		/// <summary>
		/// The property name to set in the device object.
		/// </summary>

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string ValueName { get; set; } = "";
		
		/// <summary>
		/// Step size for this property. (Only applies to numeric value types)
		/// </summary>

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string ValueStep { get; set; } = "1";
		
		/// <summary>
		/// The control type to create in the web UI.
		/// Possible options:
		/// text - Standard text input
		/// check - A checkbox (toggle)
		/// number - A number input (can be limited by valuemax/min/step
		/// ledmap - Create a LED map (custom)
		/// beamMap - Create a Lifx Beam map
		/// sectorLedMap - Create a LED map for WLED that has multiple sections
		/// select - standard select, populate the "Options" dictionary to auto-fill
		/// sectormap - Create a standard Sector map
		/// nanoleaf - Draw nano leaves
		/// hue - Draw Hue selection stuff
		/// </summary>

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string ValueType { get; set; } = "";
		
		/// <summary>
		/// A string, string dictionary containing title/value pairs to populate a select input.
		/// </summary>

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