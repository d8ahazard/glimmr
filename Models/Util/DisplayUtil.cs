using System;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;

namespace HueDream.Models.Util {
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
        public enum SystemMetric
        {
            VirtualScreenWidth = 78, // CXVIRTUALSCREEN 0x0000004E 
            VirtualScreenHeight = 79, // CYVIRTUALSCREEN 0x0000004F 
        }

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(SystemMetric metric);

        private static Size GetWindowsDisplaySize() {
            var width = GetSystemMetrics(SystemMetric.VirtualScreenWidth);
            var height = GetSystemMetrics(SystemMetric.VirtualScreenHeight);
            return new Size(width, height);
        }
        
        private static Size GetLinuxDisplaySize() {
            // Use xrandr to get size of screen located at offset (0,0).
            System.Diagnostics.Process p = new System.Diagnostics.Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.FileName = "xrandr";
            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            var match = System.Text.RegularExpressions.Regex.Match(output, @"(\d+)x(\d+)\+0\+0");
            var w = match.Groups[1].Value;
            var h = match.Groups[2].Value;
            var r = new Size(int.Parse(w,CultureInfo.InvariantCulture), int.Parse(h,CultureInfo.InvariantCulture));
            Console.WriteLine ("Display Size is {0} x {1}", w, h);
            return r;
        }
    }
}