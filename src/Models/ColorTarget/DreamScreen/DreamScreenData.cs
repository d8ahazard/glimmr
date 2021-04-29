#region

using System;
using System.Globalization;
using DreamScreenNet.Devices;

#endregion

namespace Glimmr.Models.ColorTarget.DreamScreen {
	public class DreamScreenData : IColorTargetData {
		public string Name { get; set; }
		public string Id { get; set; }
		public string Tag { get; set; } = "DreamScreen";
		public string IpAddress { get; set; }
		public int Brightness { get; set; }
		
		public int FrameDelay { get; set; }
		public bool Enable { get; set; }
		public string LastSeen { get; set; }
		public int GroupNumber { get; set; }
		public string DeviceTag { get; set; }

		public DreamScreenData(){}
		
		public DreamScreenData(DreamDevice dev) {
			Name = dev.Name;
			Id = dev.IpAddress.ToString();
			IpAddress = Id;
			Brightness = dev.Brightness;
			LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
			GroupNumber = dev.DeviceGroup;
			DeviceTag = dev.DeviceTag;
		}
		public void UpdateFromDiscovered(IColorTargetData data) {
			var dData = (DreamScreenData) data;
			Brightness = data.Brightness;
			LastSeen = data.LastSeen;
			GroupNumber = dData.Brightness;
			DeviceTag = dData.DeviceTag;
		}

		public SettingsProperty[] KeyProperties { get; set; } = {
			new("FrameDelay", "text", "Frame Delay")
		};

		public byte[] EncodeState() {
			throw new NotImplementedException();
		}
	}
}