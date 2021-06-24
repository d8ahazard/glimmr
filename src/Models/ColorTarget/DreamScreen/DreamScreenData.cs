#region

using System;
using System.Globalization;
using DreamScreenNet.Devices;

#endregion

namespace Glimmr.Models.ColorTarget.DreamScreen {
	public class DreamScreenData : IColorTargetData {
		public int GroupNumber { get; private set; }
		public string DeviceTag { get; private set; } = "DreamScreen";
		public string Name { get; set; } = "DreamScreen";
		public string Id { get; set; } = "";
		public string Tag { get; set; } = "DreamScreen";
		public string IpAddress { get; set; } = "";
		public int Brightness { get; set; } = 255;

		public bool Enable { get; set; }
		public string LastSeen { get; set; }

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

	

		public void UpdateFromDiscovered(IColorTargetData data) {
			var dData = (DreamScreenData) data;
			Brightness = dData.Brightness;
			LastSeen = data.LastSeen;
			GroupNumber = dData.GroupNumber;
			DeviceTag = dData.DeviceTag;
			if (DeviceTag.Contains("DreamScreen")) {
				Enable = false;
			}
		}

		public SettingsProperty[] KeyProperties { get; set; } = {
		};
	}
}