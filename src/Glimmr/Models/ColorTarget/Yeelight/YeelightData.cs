#region

using System;
using System.ComponentModel;
using System.Globalization;
using Glimmr.Models.Util;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget.Yeelight {
	public class YeelightData : IColorTargetData {
		/// <summary>
		///     Device brightness.
		/// </summary>
		[DefaultValue(255)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]

		public int Brightness { get; set; } = 255;

		/// <summary>
		///     Target sector for streaming.
		/// </summary>

		[DefaultValue(-1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int TargetSector { get; set; }


		public YeelightData() {
			LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
			if (!string.IsNullOrEmpty(Id)) {
				Name = StringUtil.UppercaseFirst(Id);
			}
		}

		public YeelightData(string id) {
			Id = id;
			LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
			if (!string.IsNullOrEmpty(Id)) {
				Name = StringUtil.UppercaseFirst(Id);
			}
		}

		/// <summary>
		///     Device tag.
		/// </summary>

		[DefaultValue("Yeelight")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]

		public string Tag { get; set; } = "Yeelight";

		/// <summary>
		///     Device name.
		/// </summary>
		[DefaultValue("")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]

		public string Name { get; set; } = "";

		/// <summary>
		///     Device ID.
		/// </summary>

		[DefaultValue("")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]

		public string Id { get; set; } = "";

		/// <summary>
		///     Device IP Address.
		/// </summary>
		[DefaultValue("")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]

		public string IpAddress { get; set; } = "";

		/// <summary>
		///     Enable streaming.
		/// </summary>
		[JsonProperty]
		public bool Enable { get; set; }

		/// <summary>
		///     Last time the device was seen during discovery.
		/// </summary>
		[JsonProperty]
		public string LastSeen { get; set; }


		public void UpdateFromDiscovered(IColorTargetData existing) {
			Name = existing.Name;
			IpAddress = existing.IpAddress;
		}

		/// <summary>
		///     UI Properties.
		/// </summary>
		public SettingsProperty[] KeyProperties { get; set; } = {
			new("TargetSector", "sectormap", "Target Sector")
		};
	}
}