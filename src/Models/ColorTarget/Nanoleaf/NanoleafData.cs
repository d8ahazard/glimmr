#region

using System;
using System.ComponentModel;
using System.Globalization;
using Glimmr.Models.Util;
using Nanoleaf.Client.Models.Responses;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget.Nanoleaf {
	[Serializable]
	public class NanoleafData : IColorTargetData {
		
		
		public string Name { get; set; }
		public string Id { get; set; } = "";
		public string Tag { get; set; }
		public string IpAddress { get; set; } = "";
		public int Brightness { get; set; }

		public bool Enable { get; set; }

		// Copy data from an existing leaf into this leaf...don't insert
		public string LastSeen { get; set; }

		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool MirrorX { get; set; }

		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool MirrorY { get; set; }

		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public float Rotation { get; set; }

		[JsonProperty] public int GroupNumber { get; set; }
		[JsonProperty] public int Mode { get; set; }
		[JsonProperty] public int Port { get; set; }
		[JsonProperty] public string Hostname { get; set; } = "";
		[JsonProperty] public string Token { get; set; } = "";
		[JsonProperty] public string Type { get; set; } = "";
		[JsonProperty] public string Version { get; set; } = "";
		[JsonProperty] public TileLayout Layout { get; set; }


		public NanoleafData() {
			LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
			Tag = "Nanoleaf";
			Name ??= Tag;
			if (IpAddress != null) {
				var hc = string.GetHashCode(IpAddress, StringComparison.InvariantCulture);
				Name = "Nanoleaf - " + hc.ToString(CultureInfo.InvariantCulture).Substring(0, 4);
			}

			Layout ??= new TileLayout();
		}

		public NanoleafData(Info dn) {
			LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
			Id = dn.SerialNumber;
			Name = dn.Name;
			Version = dn.FirmwareVersion;
			var hostIp = IpUtil.GetIpFromHost(Name);
			IpAddress = hostIp == null ? "" : hostIp.ToString(); 
			Tag = "Nanoleaf";
			Layout ??= new TileLayout();
		}

		public void UpdateFromDiscovered(IColorTargetData data) {
			var existingLeaf = (NanoleafData) data;
			if (existingLeaf == null) {
				throw new ArgumentException("Invalid nano data!");
			}

			if (!string.IsNullOrEmpty(existingLeaf.Token)) {
				Token = existingLeaf.Token;
			}

			// Grab the new leaf layout
			Layout.MergeLayout(existingLeaf.Layout);
			Tag = "Nanoleaf";
			Name = data.Name;
			IpAddress = data.IpAddress;
		}

		public SettingsProperty[] KeyProperties { get; set; } = {
			new("custom", "nanoleaf", "")
		};
	}
}