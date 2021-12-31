#region

using System;
using System.Globalization;
using DreamScreenNet.Devices;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget.DreamScreen;

public class DreamScreenData : IColorTargetData {
	/// <summary>
	///     Device brightness.
	/// </summary>
	[JsonProperty]
	public int Brightness { get; set; } = 255;

	/// <summary>
	///     Device group number.
	/// </summary>
	[JsonProperty]
	public int GroupNumber { get; private set; }

	/// <summary>
	///     Dreamscreen-specific device tag.
	/// </summary>
	[JsonProperty]
	public string DeviceTag { get; private set; } = "DreamScreen";

	public DreamScreenData() {
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
	}

	public DreamScreenData(DreamDevice dev) {
		Name = dev.Name;
		Id = dev.IpAddress.ToString();
		IpAddress = Id;
		Brightness = dev.Brightness;
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
		GroupNumber = dev.DeviceGroup;
		DeviceTag = dev.Type.ToString();
		if (DeviceTag.Contains("DreamScreen")) {
			Enable = false;
		}
	}

	/// <summary>
	///     Device tag.
	/// </summary>
	[JsonProperty]
	public string Tag { get; set; } = "DreamScreen";

	/// <summary>
	///     Device name.
	/// </summary>
	[JsonProperty]
	public string Name { get; set; } = "DreamScreen";

	/// <summary>
	///     Unique device ID.
	/// </summary>
	[JsonProperty]
	public string Id { get; set; } = "";

	/// <summary>
	///     Device IP Address.
	/// </summary>
	[JsonProperty]
	public string IpAddress { get; set; } = "";

	/// <summary>
	///     Enable device for streaming.
	/// </summary>
	[JsonProperty]
	public bool Enable { get; set; }

	/// <summary>
	///     Last time the device was seen during discovery.
	/// </summary>
	[JsonProperty]
	public string LastSeen { get; set; }


	public void UpdateFromDiscovered(IColorTargetData data) {
		var dData = (DreamScreenData)data;
		Brightness = dData.Brightness;
		LastSeen = data.LastSeen;
		GroupNumber = dData.GroupNumber;
		DeviceTag = dData.DeviceTag;
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
		IpAddress = dData.IpAddress;
		if (DeviceTag.Contains("DreamScreen")) {
			Enable = false;
		}
	}

	/// <summary>
	///     UI properties.
	/// </summary>
	public SettingsProperty[] KeyProperties { get; set; } = Array.Empty<SettingsProperty>();
}