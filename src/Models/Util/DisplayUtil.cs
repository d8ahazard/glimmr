#region

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace Glimmr.Models.Util {
	public static class DisplayUtil {
		[Flags]
		public enum DisplayDeviceStateFlags {
			/// <summary>The device is part of the desktop.</summary>
			AttachedToDesktop = 0x1,
			MultiDriver = 0x2,

			/// <summary>This is the primary display.</summary>
			PrimaryDevice = 0x4,

			/// <summary>Represents a pseudo device used to mirror application drawing for remoting or other purposes.</summary>
			MirroringDriver = 0x8,

			/// <summary>The device is VGA compatible.</summary>
			VGACompatible = 0x16,

			/// <summary>The device is removable; it cannot be the primary display.</summary>
			Removable = 0x20,

			/// <summary>The device has more display modes than its output devices support.</summary>
			ModesPruned = 0x8000000,
			Remote = 0x4000000,
			Disconnect = 0x2000000
		}

		public enum ScreenOrientation {
			Angle0 = 0,
			Angle180 = 2,
			Angle270 = 3,
			Angle90 = 1
		}

		public const int CaptureHeight = 480;

		public const int CaptureWidth = 640;

		public const int ENUM_CURRENT_SETTINGS = -1;
		private const int ENUM_REGISTRY_SETTINGS = -2;

		[DllImport("user32.dll")]
		public static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

		[DllImport("User32.dll")]
		public static extern int EnumDisplayDevices(string? lpDevice, int iDevNum, ref DisplayDevice lpDisplayDevice,
			int dwFlags);

		public static Rectangle GetDisplaySize() {
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				return GetWindowsDisplaySize();
			}

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
				return GetLinuxDisplaySize();
			}

			return new Rectangle();
		}

		[DllImport("user32.dll")]
		private static extern int GetSystemMetrics(SystemMetric metric);


		private static Rectangle GetWindowsDisplaySize() {
			var left = 0;
			var right = 0;
			var top = 0;
			var bottom = 0;
			var width = 0;
			var height = 0;
			// Enumerate system display devices
			var devIdx = 0;
			while (true) {
				var deviceData = new DisplayDevice {cb = Marshal.SizeOf(typeof(DisplayDevice))};
				if (EnumDisplayDevices(null, devIdx, ref deviceData, 0) != 0) {
					// Get the position and size of this particular display device
					var devMode = new DEVMODE();
					if (EnumDisplaySettings(deviceData.DeviceName, ENUM_CURRENT_SETTINGS, ref devMode)) {
						Log.Debug("Enumerating monitor: " + deviceData.DeviceName);
						// Update the virtual screen dimensions
						left = Math.Min(left, devMode.dmPositionX);
						top = Math.Min(top, devMode.dmPositionY);
						right = Math.Max(right, devMode.dmPositionX + devMode.dmPelsWidth);
						bottom = Math.Max(bottom, devMode.dmPositionY + devMode.dmPelsHeight);
						width = left - right;
						height = top - bottom;
					}

					devIdx++;
				} else {
					break;
				}
			}

			width = Math.Abs(width);
			height = Math.Abs(height);
			var rect = new Rectangle(left, top, width, height);
			return rect;
		}

		private static Rectangle GetLinuxDisplaySize() {
			var r = new Rectangle();
			var output = string.Empty;
			try {
				var p = new Process {
					StartInfo = {UseShellExecute = false, RedirectStandardOutput = true, FileName = "xrandr"}
				};
				p.Start();
				output = p.StandardOutput.ReadToEnd();
				p.WaitForExit();
				p.Dispose();
			} catch (Win32Exception e) {
				Log.Warning("Error running xrandr...possibly docker: " + e.Message);
			}

			if (string.IsNullOrEmpty(output)) {
				return r;
			}

			try {
				var match = Regex.Match(output, @"(\d+)x(\d+)\+0\+0");
				var w = match.Groups[1].Value;
				var h = match.Groups[2].Value;
				r = new Rectangle(0, 0, int.Parse(w, CultureInfo.InvariantCulture),
					int.Parse(h, CultureInfo.InvariantCulture));
				Console.WriteLine("Display Size is {0} x {1}", w, h);
			} catch (FormatException) {
				//Log.Debug("Format exception, probably we have no screen.");
			}

			return r;
		}

		public static List<MonitorInfo> GetMonitors() {
			var monitors = new List<MonitorInfo>();
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				return monitors;
			}

			var devIdx = 0;
			while (true) {
				var deviceData = new DisplayDevice {cb = Marshal.SizeOf(typeof(DisplayDevice))};
				if (EnumDisplayDevices(null, devIdx, ref deviceData, 0) != 0) {
					// Get the position and size of this particular display device
					var devMode = new DEVMODE();
					if (EnumDisplaySettings(deviceData.DeviceName, ENUM_CURRENT_SETTINGS, ref devMode)) {
						JsonConvert.SerializeObject(devMode);
						monitors.Add(new MonitorInfo(deviceData, devMode));
					}

					devIdx++;
				} else {
					break;
				}
			}

			return monitors;
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
		public struct DisplayDevice {
			[MarshalAs(UnmanagedType.U4)] public int cb;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
			public string DeviceName;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
			public string DeviceString;

			[MarshalAs(UnmanagedType.U4)] public DisplayDeviceStateFlags StateFlags;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
			public string DeviceID;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
			public string DeviceKey;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct DEVMODE {
			private const int CCHDEVICENAME = 0x20;
			private const int CCHFORMNAME = 0x20;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
			public string dmDeviceName;

			public short dmSpecVersion;
			public short dmDriverVersion;
			public short dmSize;
			public short dmDriverExtra;
			public int dmFields;
			public int dmPositionX;
			public int dmPositionY;
			public ScreenOrientation dmDisplayOrientation;
			public int dmDisplayFixedOutput;
			public short dmColor;
			public short dmDuplex;
			public short dmYResolution;
			public short dmTTOption;
			public short dmCollate;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
			public string dmFormName;

			public short dmLogPixels;
			public int dmBitsPerPel;
			public int dmPelsWidth;
			public int dmPelsHeight;
			public int dmDisplayFlags;
			public int dmDisplayFrequency;
			public int dmICMMethod;
			public int dmICMIntent;
			public int dmMediaType;
			public int dmDitherType;
			public int dmReserved1;
			public int dmReserved2;
			public int dmPanningWidth;
			public int dmPanningHeight;
		}

		private enum SystemMetric {
			VirtualScreenWidth = 78, // CXVIRTUALSCREEN 0x0000004E 
			VirtualScreenHeight = 79 // CYVIRTUALSCREEN 0x0000004F
		}
	}

	[Serializable]
	public class MonitorInfo {
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		[DefaultValue(true)]
		public bool Enable { get; set; }

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		[DefaultValue(0)]
		public int DmPelsHeight { get; set; }

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		[DefaultValue(0)]
		public int DmPelsWidth { get; set; }

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		[DefaultValue(0)]
		public int DmPositionX { get; set; }

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		[DefaultValue(0)]
		public int DmPositionY { get; set; }

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		[DefaultValue("")]
		public string DeviceKey { get; set; }

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		[DefaultValue("")]
		public string DeviceName { get; set; }

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		[DefaultValue("")]
		public string DeviceString { get; set; }

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		[DefaultValue("")]
		public string Id { get; set; }

		public MonitorInfo() {
		}

		public MonitorInfo(DisplayUtil.DisplayDevice device, DisplayUtil.DEVMODE mode) {
			DeviceName = device.DeviceName;
			DeviceString = device.DeviceString;
			Id = device.DeviceID;
			DeviceKey = device.DeviceKey;
			DmPositionX = mode.dmPositionX;
			DmPositionY = mode.dmPositionY;
			DmPelsHeight = mode.dmPelsHeight;
			DmPelsWidth = mode.dmPelsWidth;
			Enable = false;
		}
	}
}