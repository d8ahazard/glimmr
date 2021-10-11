#region

using System;
using System.Collections.Generic;
using Glimmr.Models.ColorTarget.Adalight;
using Glimmr.Models.ColorTarget.DreamScreen;
using Glimmr.Models.ColorTarget.Glimmr;
using Glimmr.Models.ColorTarget.Hue;
using Glimmr.Models.ColorTarget.Led;
using Glimmr.Models.ColorTarget.Lifx;
using Glimmr.Models.ColorTarget.Nanoleaf;
using Glimmr.Models.ColorTarget.OpenRgb;
using Glimmr.Models.ColorTarget.Wled;
using Glimmr.Models.ColorTarget.Yeelight;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Annotations;

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
	
	[SwaggerSubType(typeof(AdalightData))]
	[SwaggerSubType(typeof(DreamScreenData))]
	[SwaggerSubType(typeof(GlimmrData))]
	[SwaggerSubType(typeof(HueData))]
	[SwaggerSubType(typeof(LedData))]
	[SwaggerSubType(typeof(LifxData))]
	[SwaggerSubType(typeof(NanoleafData))]
	[SwaggerSubType(typeof(OpenRgbData))]
	[SwaggerSubType(typeof(WledData))]
	[SwaggerSubType(typeof(YeelightData))]
	public interface IColorTargetData {
		/// <summary>
		/// If set, Glimmr will attempt to control this device.
		/// </summary>
		public bool Enable { get;  set;}
		
		/// <summary>
		/// Unique device tag.
		/// </summary>
		public string Tag { get; set; }

		/// <summary>
		/// An array of properties that will be auto-filled in the web UI.
		/// </summary>
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
		public SettingsProperty[] KeyProperties { get; set; }

		/// <summary>
		/// A unique device identifier.
		/// </summary>
		public string Id { get;  set;}
		
		/// <summary>
		/// The device IP address.
		/// </summary>
		public string IpAddress { get; set; }
		
		/// <summary>
		/// The last time the device was seen via device discovery.
		/// </summary>
		public string LastSeen { get;  set;}

		/// <summary>
		/// The device name.
		/// </summary>
		public string Name { get;  set;}
		public void UpdateFromDiscovered(IColorTargetData data);
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
		public Dictionary<string, string> Options { get; set; }

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