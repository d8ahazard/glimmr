using Corsair.CUE.SDK;
using Newtonsoft.Json;
using Serilog;

namespace Glimmr.Models.ColorTarget.Corsair {
	public class CorsairData : IColorTargetData {
		[JsonProperty]public string Name { get; set; }
		[JsonProperty]public string Id { get; set; }
		[JsonProperty] public string Tag { get; set; } = "Corsair";
		[JsonProperty]public string DeviceTag { get; set; }
		[JsonProperty]public string IpAddress { get; set; }
		[JsonProperty]public int Brightness { get; set; }
		[JsonProperty]public int DeviceIndex { get; set; }
		[JsonProperty]public bool Enable { get; set; }
		[JsonProperty]public string LastSeen { get; set; }
		[JsonProperty]public int Offset { get; set; }
		[JsonProperty]public bool Reverse { get; set; }
		[JsonProperty]public int LedCount { get; set; }
		public SettingsProperty[] KeyProperties { get; set; }= {
			new("custom","ledmap",""),
			new("Offset", "text", "Device Offset")
		};

		public CorsairData() {
		}

		public CorsairData(int id, CorsairDeviceInfo info) {
			Log.Debug("Loading info: " + JsonConvert.SerializeObject(info));
			Id = "Corsair" + id;
			Name = info.model;
			LedCount = info.ledsCount;
			Log.Debug("Adding tag...");
			DeviceTag = info.type.ToString();
			Log.Debug("Done.");
			DeviceIndex = id;
		}

		public void UpdateFromDiscovered(IColorTargetData data) {
			var cd = (CorsairData) data;
			Name = cd.Name;
			LedCount = cd.LedCount;
			DeviceTag = cd.DeviceTag;
			DeviceIndex = cd.DeviceIndex;
		}

		
	}

}