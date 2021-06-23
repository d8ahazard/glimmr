#region

using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using Glimmr.Models.Util;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models {
	public class SystemData {
		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

		// Full screen region
		[JsonProperty] public static Rectangle MonitorRegion => DisplayUtil.GetDisplaySize();

		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool AutoDisabled { get; set; }

		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool AutoRemoveDevices { get; set; }

		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool AutoUpdate { get; set; }

		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool DefaultSet { get; set; }

		[DefaultValue(true)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool EnableAutoDisable { get; set; } = true;

		[DefaultValue(true)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool EnableLetterBox { get; set; } = true;

		[DefaultValue(true)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool EnablePillarBox { get; set; } = true;

		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool ShowSource { get; set; }

		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool TestRazer { get; set; }

		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool UseCenter { get; set; }

		[DefaultValue(.5f)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public float AudioGain { get; set; } = .5f;

		[DefaultValue(.025f)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public float AudioMin { get; set; } = .025f;

		[DefaultValue(.0f)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public float AudioRotationLower { get; set; }

		[DefaultValue(.0f)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public float AudioRotationSensitivity { get; set; }

		[DefaultValue(.0f)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public float AudioRotationSpeed { get; set; }

		[DefaultValue(1f)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public float AudioRotationUpper { get; set; } = 1f;

		[DefaultValue(.01f)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public float AudioThreshold { get; set; } = .01f;

		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int AmbientMode { get; set; }

		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int AmbientShow { get; set; }

		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int AudioMap { get; set; }

		[DefaultValue(30)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int AutoDisableDelay { get; set; }

		[DefaultValue(60)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int AutoDiscoveryFrequency { get; set; }

		// How many days to wait to auto-remove not-seen devices.
		[DefaultValue(7)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int AutoRemoveDevicesAfter { get; set; } = 7;

		[DefaultValue(2)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int AutoUpdateTime { get; set; }

		[DefaultValue(96)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int BottomCount { get; set; } = 96;

		[DefaultValue(255)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int Brightness { get; set; }

		[DefaultValue(1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int CamType { get; set; } = 1;

		[DefaultValue(2)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int CaptureMode { get; set; } = 2;

		[DefaultValue(15)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int CropDelay { get; set; }

		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int DeviceGroup { get; set; }


		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int DeviceMode { get; set; }

		[DefaultValue(10)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int DiscoveryTimeout { get; set; }


		[DefaultValue(10)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int HSectors { get; set; } = 10;

		// Values for general LED settings
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int LedCount => LeftCount + RightCount + TopCount + BottomCount;

		[DefaultValue(54)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int LeftCount { get; set; } = 54;

		[DefaultValue(255)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int MinBrightness { get; set; } = 255;

		[DefaultValue(6742)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int OpenRgbPort { get; set; } = 6742;

		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int PreviewMode { get; set; }

		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int PreviousMode { get; set; }

		[DefaultValue(1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int RecId { get; set; } = 1;

		[DefaultValue(54)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int RightCount { get; set; } = 54;

		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int SaturationBoost { get; set; }

		// Screen capture mode. 0="region", 1="monitor". 1 is only available for windows users.
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int ScreenCapMode { get; set; }

		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int SectorCount {
			get {
				if (UseCenter) {
					return HSectors * VSectors;
				}

				return HSectors + HSectors + VSectors + VSectors - 4;
			}
		}

		[DefaultValue(96)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int TopCount { get; set; } = 96;

		// USB index to use for cam/HDMI
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int UsbSelection { get; set; }


		[DefaultValue(6)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int VSectors { get; set; } = 6;

		// Selected screen region
		[JsonProperty] public Rectangle CaptureRegion { get; set; }

		[JsonProperty] public string AmbientColor { get; set; } = "FFFFFF";

		[DefaultValue("Dreamscreen4K")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string DevType { get; set; } = "Dreamscreen4K";

		[DefaultValue("")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string DsIp { get; set; } = "";

		[DefaultValue("")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string GroupName { get; set; } = "";

		[DefaultValue("")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Name { get; set; } = "";

		[DefaultValue("127.0.0.1")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string OpenRgbIp { get; set; } = "127.0.0.1";

		[DefaultValue("")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string RecDev { get; set; } = "";


		//TODO: Make getter for this always retrieve same value used by setup script
		[DefaultValue("")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Serial { get; set; } = "";

		[DefaultValue("dark")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Theme { get; set; } = "dark";

		[DefaultValue("US/Central")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string TimeZone { get; set; } = "US/Central";

		public void SetDefaults() {
			Brightness = 255;
			DiscoveryTimeout = 10;
			AutoDiscoveryFrequency = 60;
			CropDelay = 15;
			DeviceMode = 0;
			AutoUpdateTime = 2;
			CaptureRegion = DisplayUtil.GetDisplaySize();
			DefaultSet = true;
		}
	}
}