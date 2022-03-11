#region

using System;
using System.ComponentModel;
using System.Globalization;
using Glimmr.Models.Util;
using Nanoleaf.Client.Models.Responses;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget.Nanoleaf;

[Serializable]
public class NanoleafData : IColorTargetData {
	
	/// <summary>
	///     Device Brightness.
	/// </summary>
	[DefaultValue(100)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int Brightness { get; set; }

	/// <summary>
	///     Port used for communication.
	/// </summary>
	[JsonProperty]
	public int Port { get; set; }

	/// <summary>
	///     Device host name.
	/// </summary>
	[JsonProperty]
	public string Hostname { get; set; } = "";

	/// <summary>
	///     Token used for control, retrieved after authorization.
	/// </summary>
	[JsonProperty]
	public string Token { get; set; } = "";

	/// <summary>
	///     Nanoleaf type.
	/// </summary>
	[JsonProperty]
	public string Type { get; set; } = "";

	/// <summary>
	///     Device protocol version.
	/// </summary>
	[JsonProperty]
	public string Version { get; set; } = "";

	/// <summary>
	///     Layout of device tiles.
	/// </summary>
	[JsonProperty]
	public TileLayout Layout { get; set; }


	public NanoleafData() {
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
		Tag = "Nanoleaf";
		Name ??= Tag;
		if (!string.IsNullOrEmpty(IpAddress)) {
			var hc = IpAddress.GetHashCode();
			Name = "Nanoleaf - " + hc.ToString(CultureInfo.InvariantCulture)[..4];
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

	/// <summary>
	///     Device tag.
	/// </summary>
	public string Tag { get; set; }


	/// <summary>
	///     Device name.
	/// </summary>
	public string Name { get; set; }

	/// <summary>
	///     Device ID.
	/// </summary>
	public string Id { get; set; } = "";

	/// <summary>
	///     Device IP address.
	/// </summary>
	public string IpAddress { get; set; } = "";

	/// <summary>
	///     Enable device for streaming.
	/// </summary>
	public bool Enable { get; set; }

	/// <summary>
	///     Last time the device was seen during discovery.
	/// </summary>
	public string LastSeen { get; set; }

	public void UpdateFromDiscovered(IColorTargetData data) {
		var existingLeaf = (NanoleafData)data;
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

	/// <summary>
	///     UI Properties.
	/// </summary>
	public SettingsProperty[] KeyProperties { get; set; } = {
		new("custom", "nanoleaf", "")
	};
}