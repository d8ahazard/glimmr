using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Emgu.Util;
using Serilog;

namespace Glimmr.Models.Util {
	public static class SystemUtil {
		public static void Reboot() {
			Log.Debug("Rebooting");
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
				Process.Start("reboot");
			} else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				Process.Start("shutdown", "/r /t 0");
			}
		}

		public static void Update() {
			Log.Debug("Updating");
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
				Process.Start("/bin/bash","/etc/init.d/update_glimmr.sh");
			} else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				Process.Start("../script/update_win.bat");
			}
		}

		public static void Restart() {
			Log.Debug("Restarting glimmr");
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
				Process.Start("service", "glimmr restart");
			} else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				Process.Start("net", "stop glimmr");
				Process.Start("net", "start glimmr");
			}
		}

		public static void Shutdown() {
			Log.Debug("Shutting down");
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
				Process.Start("shutdown");
			} else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				Process.Start("shutdown", "/s /t 0");
			}
		}

		public static string GetUserDir() {
			var userDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			if (userDir == String.Empty && RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
				userDir = "/etc/";
			}
			var fullPath = Path.Combine(userDir, "Glimmr");
			if (!Directory.Exists(fullPath)) {
				try {
					var res = Directory.CreateDirectory(fullPath);
					return fullPath;
				} catch (Exception e) {
					Log.Warning("Exception creating userdata dir: " + e.Message);
					return String.Empty;
				}
			}
			return String.Empty;
		}
	}
}