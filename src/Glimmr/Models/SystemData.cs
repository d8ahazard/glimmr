#region

using System;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using Glimmr.Enums;
using Glimmr.Models.Util;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models;

public class SystemData {
	/// <summary>
	///     Whether or not the OS is windows (Auto-set).
	/// </summary>
	[DefaultValue(false)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

	/// <summary>
	///     If the system is currently auto-disabled due to no input.
	/// </summary>
	[DefaultValue(false)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool AutoDisabled { get; set; }

	/// <summary>
	///     If enabled, devices will be automatically removed after the specified time.
	/// </summary>
	[DefaultValue(false)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool AutoRemoveDevices { get; set; }

	/// <summary>
	///     If enabled, Glimmr will automatically update itself daily.
	/// </summary>
	[DefaultValue(false)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool AutoUpdate { get; set; }

	/// <summary>
	///     Set on first-time initialization. Don't change this.
	/// </summary>
	[DefaultValue(false)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool DefaultSet { get; set; }

	/// <summary>
	///     If set, wired LED strips will have their brightness automatically adjusted,
	///     a la WLED.
	/// </summary>
	[DefaultValue(true)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool EnableAutoBrightness { get; set; } = true;

	/// <summary>
	///     If set, streaming will be automatically stopped when no input is detected.
	/// </summary>
	[DefaultValue(true)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool EnableAutoDisable { get; set; } = true;

	/// <summary>
	///     If set, horizontal black bars will be cropped when detected.
	/// </summary>
	[DefaultValue(true)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool EnableLetterBox { get; set; } = true;


	/// <summary>
	///     If set, vertical black bars will be cropped when detected.
	/// </summary>
	[DefaultValue(true)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool EnablePillarBox { get; set; } = true;


	/// <summary>
	///     If set, rainbow wipe will not be played on application startup.
	/// </summary>
	[DefaultValue(false)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool SkipDemo { get; set; }

	/// <summary>
	///     If set, introduction/tour will not run on UI load.
	/// </summary>
	[DefaultValue(false)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool SkipTour { get; set; }

	/// <summary>
	///     If set, sectors will also be collected from teh center of the screen, not just the perimeter.
	/// </summary>
	[DefaultValue(false)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool UseCenter { get; set; }

	/// <summary>
	///     Type of camera used for capture.
	///     0 = Raspberry pi camera module
	///     1 = USB webcam
	/// </summary>
	[DefaultValue(1)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public CameraType CamType { get; set; } = CameraType.WebCam;

	/// <summary>
	///     Currently selected capture mode for video input.
	///     Camera = 1
	///     Hdmi = 2
	///     Screen = 3
	/// </summary>
	[DefaultValue(2)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public CaptureMode CaptureMode { get; set; } = CaptureMode.Hdmi;

	/// <summary>
	///     The currently selected device mode.
	/// </summary>
	[DefaultValue(0)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public DeviceMode DeviceMode { get; set; }

	/// <summary>
	///     The previous device mode before Auto-disable was activated.
	/// </summary>
	[DefaultValue(DeviceMode.Off)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public DeviceMode PreviousMode { get; set; }

	/// <summary>
	///     Temperature units.
	/// </summary>
	[DefaultValue("0")]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public DeviceUnits Units { get; set; } = DeviceUnits.Imperial;

	/// <summary>
	///     Input amps of power supply.
	/// </summary>
	[DefaultValue(3)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public float AblAmps { get; set; } = 2f;

	/// <summary>
	///     Input voltage of power supply.
	/// </summary>
	[DefaultValue(5)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public float AblVolts { get; set; } = 5f;

	/// <summary>
	///     How much to increase input audio volume.
	/// </summary>
	[DefaultValue(.5f)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public float AudioGain { get; set; } = .6f;

	/// <summary>
	///     Low cutoff for audio detection, values below this will not be displayed.
	/// </summary>
	[DefaultValue(.025f)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public float AudioMin { get; set; } = .01f;
	
	/// <summary>
	///     Low cutoff for audio detection, values below this will not be displayed.
	/// </summary>
	[DefaultValue(0f)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public float AudioRotationLower { get; set; }
	
	/// <summary>
	///     Low cutoff for audio detection, values below this will not be displayed.
	/// </summary>
	[DefaultValue(1.0f)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public float AudioRotationUpper { get; set; } = 1.0f;
	
	/// <summary>
	///     Low cutoff for audio detection, values below this will not be displayed.
	/// </summary>
	[DefaultValue(.5f)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public float AudioRotationSpeed { get; set; } = .5f;
	
	/// <summary>
	///     Low cutoff for audio detection, values below this will not be displayed.
	/// </summary>
	[DefaultValue(.5f)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public float AudioRotationSensitivity { get; set; } = .5f;

	/// <summary>
	///     Current ambient scene.
	/// </summary>
	[DefaultValue(0)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int AmbientScene { get; set; }

	/// <summary>
	///     Current audio scene.
	/// </summary>
	[DefaultValue(0)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int AudioScene { get; set; }

	/// <summary>
	///     How long to wait (in seconds) before disabling streaming when no
	///     input is detected, when auto-disable is active.
	/// </summary>
	[DefaultValue(30)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int AutoDisableDelay { get; set; }

	/// <summary>
	///     Delay (in minutes) between execution of auto-discovery.
	/// </summary>
	[DefaultValue(60)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int AutoDiscoveryFrequency { get; set; }

	/// <summary>
	///     If enabled,
	/// </summary>
	// How many days to wait to auto-remove not-seen devices, when auto-remove
	// is enabled.
	[DefaultValue(7)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int AutoRemoveDevicesAfter { get; set; } = 7;

	/// <summary>
	///     How frequently to automatically send updated system data to the UI.
	/// </summary>
	[DefaultValue(2)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int AutoUpdateTime { get; set; }

	/// <summary>
	///     Speed at which to attempt discovering Adalight devices.
	/// </summary>
	[DefaultValue(115200)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int BaudRate { get; set; } = 115200;

	/// <summary>
	///     Colors below this brightness will be considered "black".
	///     (Max 255)
	/// </summary>
	[DefaultValue(7)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int BlackLevel { get; set; } = 7;

	/// <summary>
	///     Number of LEDs along the bottom of the screen.
	/// </summary>
	[DefaultValue(96)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int BottomCount { get; set; } = 96;

	/// <summary>
	///     Value cropping checks use to determine what is "black" and should
	///     be cropped.
	/// </summary>
	[DefaultValue(7)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int CropBlackLevel { get; set; } = 7;

	/// <summary>
	///     The number of frames required for detection before cropping is
	///     activated, if enabled.
	/// </summary>
	[DefaultValue(15)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int CropDelay { get; set; }

	/// <summary>
	///     How long to wait before canceling discovery tasks, in seconds.
	/// </summary>
	[DefaultValue(10)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int DiscoveryTimeout { get; set; }

	/// <summary>
	///     Number of horizontal sectors around the screen
	/// </summary>
	[DefaultValue(10)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int HSectors { get; set; } = 10;

	/// <summary>
	///     Number of LEDs for the "master grid". This is auto-computed via left/right/top/bottom counts.
	/// </summary>
	// Values for general LED settings
	[DefaultValue(0)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int LedCount => LeftCount + RightCount + TopCount + BottomCount;

	/// <summary>
	///     Number of LEDs along the left side of the screen.
	/// </summary>
	[DefaultValue(54)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int LeftCount { get; set; } = 54;

	/// <summary>
	///     Level to log the logging at. 0 = debug, 1 = inf
	/// </summary>
	[DefaultValue(1)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int LogLevel { get; set; }

	/// <summary>
	///     Port to use for OpenRGB communication. (Default is 6742)
	/// </summary>
	[DefaultValue(6742)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int OpenRgbPort { get; set; } = 6742;

	/// <summary>
	///     Image preview mode for the web UI.
	///     0 = None
	///     1 = LED
	///     2 = Sectors
	/// </summary>
	[DefaultValue(0)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int PreviewMode { get; set; }


	/// <summary>
	///     Number of LEDs along the right side of the screen.
	/// </summary>
	[DefaultValue(54)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int RightCount { get; set; } = 54;

	/// <summary>
	///     Total number of sectors available. Is auto-computed based on sector counts and
	///     whether center sectors are enabled.
	/// </summary>
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

	/// <summary>
	///     Number of LEDs along the top of the screen.
	/// </summary>
	[DefaultValue(96)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int TopCount { get; set; } = 96;

	/// <summary>
	///     Currently selected USB device for HDMI/camera capture.
	/// </summary>
	[DefaultValue(0)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int UsbSelection { get; set; }


	/// <summary>
	///     Number of vertical sectors along the left/right of screen.
	/// </summary>
	[DefaultValue(6)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int VSectors { get; set; } = 6;

	/// <summary>
	///     Currently selected streaming mode.
	/// </summary>
	[DefaultValue(15)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]

	public StreamMode StreamMode { get; set; } = StreamMode.Udp;

	/// <summary>
	///     Current ambient color used when AmbientShow is set to "solid".
	/// </summary>
	[JsonProperty]
	public string AmbientColor { get; set; } = "FFFFFF";

	/// <summary>
	///     Device name (should be device hostname)
	/// </summary>
	[DefaultValue("")]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public string DeviceName { get; set; } = "";

	/// <summary>
	///     Target DreamScreen device to receive color data from.
	///     (Streaming mode must be set to 0/DreamScreen)
	/// </summary>
	[DefaultValue("")]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public string DsIp { get; set; } = "";

	/// <summary>
	///     Local IP Address.
	/// </summary>
	[JsonProperty]
	public string IpAddress => IpUtil.GetLocalIpAddress();

	/// <summary>
	///     IP address to use for OpenRGB communication.
	/// </summary>
	[DefaultValue("127.0.0.1")]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public string OpenRgbIp { get; set; } = "127.0.0.1";


	/// <summary>
	///     Name of the selected audio device for audio and audio/video capture.
	/// </summary>
	[DefaultValue("")]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public string RecDev { get; set; } = "";

	/// <summary>
	///     Web UI theme
	///     (dark/light)
	/// </summary>
	[DefaultValue("dark")]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Theme { get; set; } = "dark";

	/// <summary>
	///     Time zone to use for automatic updates.
	/// </summary>
	[DefaultValue("US/Central")]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public string TimeZone { get; set; } = "US/Central";

	/// <summary>
	///     Version string based on current build + github revision.
	/// </summary>
	[JsonProperty]
	public string Version {
		get {
			var val = "0.0.0.0";
			var assembly = Assembly.GetEntryAssembly();
			if (assembly == null) {
				return val;
			}

			var attrib = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
			if (attrib != null) {
				val = attrib.InformationalVersion;
			}

			return val;
		}
	}

	/// <summary>
	///     A unique device ID based on MAC address, or random if no NIC accessible.
	/// </summary>
	[DefaultValue("")]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public string? DeviceId { get; set; } = "";

	public SystemData() {
		CheckDeviceVariables();
	}

	public void SetDefaults() {
		DiscoveryTimeout = 10;
		AutoDiscoveryFrequency = 60;
		CropDelay = 15;
		DeviceMode = DeviceMode.Off;
		AutoUpdateTime = 2;
		AblAmps = 2;
		DefaultSet = true;
		AudioGain = .6f;
		AudioMin = .01f;
		BaudRate = 115200;
		CropBlackLevel = 7;
		BlackLevel = 7;
		CheckDeviceVariables();
		EnableAutoBrightness = true;
		LogLevel = 1;
	}

	public string CheckDeviceVariables() {
		var id = DeviceId;
		if (string.IsNullOrEmpty(id)) {
			id = NetworkInterface
				.GetAllNetworkInterfaces()
				.Where(nic =>
					nic.OperationalStatus == OperationalStatus.Up &&
					nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
				.Select(nic => nic.GetPhysicalAddress().ToString())
				.FirstOrDefault();
			DeviceId = id;
			if (!string.IsNullOrEmpty(id)) {
				return id;
			}

			var rand = new Random();
			const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
			id = new string(Enumerable.Repeat(chars, 6)
				.Select(s => s[rand.Next(s.Length)]).ToArray());
		}

		DeviceId = id;
		if (!SystemUtil.IsDocker()) {
			DeviceName = Environment.MachineName;
			if (string.IsNullOrEmpty(DeviceName)) {
				DeviceName = Dns.GetHostName();
			}
		} else {
			DeviceName = "Glimmr-" + id[..3];
		}

		return id;
	}
}