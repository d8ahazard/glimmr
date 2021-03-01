using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using WindowsDisplayAPI;
using Serilog;

namespace Glimmr.Models.Util {
    public static class DisplayUtil {

        public const int CaptureWidth = 640;
        public const int CaptureHeight = 480;

        public enum ScreenOrientation {
           Angle0 = 0,
           Angle180 = 2,
           Angle270 = 3,
           Angle90 = 1
        }
        
        [Flags()]
        public enum DisplayDeviceStateFlags : int
        {
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

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DisplayDevice
        {
            [MarshalAs(UnmanagedType.U4)]
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            [MarshalAs(UnmanagedType.U4)]
            public DisplayDeviceStateFlags StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DEVMODE
        {
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

        [DllImport("user32.dll")]
        public static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        public const int ENUM_CURRENT_SETTINGS = -1;
        const int ENUM_REGISTRY_SETTINGS = -2;

        [DllImport("User32.dll")]
        public static extern int EnumDisplayDevices(string lpDevice, int iDevNum, ref DisplayDevice lpDisplayDevice, int dwFlags);

        public static Size GetDisplaySize() {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                return GetWindowsDisplaySize();
            }
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                return GetLinuxDisplaySize();
            }

            return new Size(0, 0);
        }

        private enum SystemMetric {
            VirtualScreenWidth = 78, // CXVIRTUALSCREEN 0x0000004E 
            VirtualScreenHeight = 79, // CYVIRTUALSCREEN 0x0000004F
            MonitorCount = 80,
        }

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(SystemMetric metric);
        

        private static Size GetWindowsDisplaySize() {
            var width = GetSystemMetrics(SystemMetric.VirtualScreenWidth);
            var height = GetSystemMetrics(SystemMetric.VirtualScreenHeight);
            var currentDpi = 0;
            #if NETFX_CORE
                currentDpi = (int)Microsoft.Win32.Registry.GetValue("HKEY_CURRENT_USER\\Control Panel\\Desktop\\WindowMetrics", "AppliedDPI", 0);
            #endif
            var scaleTable = new Dictionary<int, float> {
                [96] = 1.00f,
                [120] = 1.25f,
                [144] = 1.50f,
                [192] = 2.00f,
                [240] = 2.50f,
                [288] = 3.00f,
                [384] = 4.00f,
                [480] = 5.00f
            };
            var newScale = 1.00f;
            if (scaleTable.ContainsKey(currentDpi)) newScale = scaleTable[currentDpi];
            var tWidth = width * newScale;
            var tHeight = height * newScale;
            return new Size((int) tWidth, (int) tHeight);
        }

        public static List<DisplayAdapter> GetMonitorInfo() {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? DisplayAdapter.GetDisplayAdapters().ToList() : null;
        }
        
        private static Size GetLinuxDisplaySize() {
            var p = new Process {
                StartInfo = {UseShellExecute = false, RedirectStandardOutput = true, FileName = "xrandr"}
            };
            p.Start();
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            p.Dispose();
            Log.Debug("Output from screen check: " + output);
            var r = new Size(0,0);
            try {
                var match = Regex.Match(output, @"(\d+)x(\d+)\+0\+0");
                var w = match.Groups[1].Value;
                var h = match.Groups[2].Value;
                r = new Size(int.Parse(w, CultureInfo.InvariantCulture),
                    int.Parse(h, CultureInfo.InvariantCulture));
                Console.WriteLine ("Display Size is {0} x {1}", w, h);
            } catch (FormatException) {
                Log.Debug("Format exception, probably we have no screen.");
            }

            return r;
        }
    }
}