#region

using System;
using System.Globalization;
using DreamScreenNet.Devices;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget.DreamScreen {
	public class DreamScreenData : IColorTargetData {
		[JsonProperty] public int Brightness { get; set; } = 255;
		[JsonProperty] public int GroupNumber { get; private set; }
		[JsonProperty] public string DeviceTag { get; private set; } = "DreamScreen";
		[JsonProperty] public string Tag { get; set; } = "DreamScreen";

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

		[JsonProperty] public string Name { get; set; } = "DreamScreen";
		[JsonProperty] public string Id { get; set; } = "";
		[JsonProperty] public string IpAddress { get; set; } = "";

		[JsonProperty] public bool Enable { get; set; }
		[JsonProperty] public string LastSeen { get; set; }


		public void UpdateFromDiscovered(IColorTargetData data) {
			var dData = (DreamScreenData) data;
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

		public SettingsProperty[] KeyProperties { get; set; } = {
		};
	}
}