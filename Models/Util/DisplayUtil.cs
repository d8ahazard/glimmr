using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using Serilog;

namespace Glimmr.Models.Util {
    public static class DisplayUtil {

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
            VirtualScreenHeight = 79 // CYVIRTUALSCREEN 0x0000004F 
        }

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(SystemMetric metric);

        private static Size GetWindowsDisplaySize() {
            var width = GetSystemMetrics(SystemMetric.VirtualScreenWidth);
            var height = GetSystemMetrics(SystemMetric.VirtualScreenHeight);
            var currentDpi = (int)Microsoft.Win32.Registry.GetValue("HKEY_CURRENT_USER\\Control Panel\\Desktop\\WindowMetrics", "AppliedDPI", 0);
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
        
        private static Size GetLinuxDisplaySize() {
            var p = new System.Diagnostics.Process {
                StartInfo = {UseShellExecute = false, RedirectStandardOutput = true, FileName = "xrandr"}
            };
            p.Start();
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            p.Dispose();
            Log.Debug("Output from screen check: " + output);
            var r = new Size(0,0);
            try {
                var match = System.Text.RegularExpressions.Regex.Match(output, @"(\d+)x(\d+)\+0\+0");
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