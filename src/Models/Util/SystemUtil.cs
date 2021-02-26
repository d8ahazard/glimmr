using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Glimmr.Models.ColorTarget;
using Serilog;

namespace Glimmr.Models.Util {
	public static class SystemUtil {
		public static void Reboot() {
			Log.Debug("Rebooting");
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
				Process.Start("shutdown", "-r now");
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
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
				Log.Debug("Platform is linux.");
				userDir = "/etc/";
			}

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) Log.Debug("Platform is Windows");
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) Log.Debug("Platform is OSX");
			var fullPath = Path.Combine(userDir, "Glimmr");
			Log.Debug("Fullpath is " + fullPath);
			if (!Directory.Exists(fullPath)) {
				try {
					var res = Directory.CreateDirectory(fullPath);
					Log.Debug("Returning: " + fullPath);
					return fullPath;
				} catch (Exception e) {
					Log.Warning("Exception creating userdata dir: " + e.Message);
					return String.Empty;
				}
			}

			if (Directory.Exists(fullPath)) return fullPath;
			return String.Empty;
		}
		
		public static List<string> GetDiscoverables() {
			return AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes())
				.Where(x => typeof(IColorDiscovery).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)
				.Select(x => x.FullName).ToList();
		}
		
		public static List<string> GetColorTargets() {
			return AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes())
				.Where(x => typeof(IColorTarget).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)
				.Select(x=>x.FullName).ToList();
		}
		
		public static List<string> GetColorTargetAgents() {
			return AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes())
				.Where(x => typeof(IColorTargetAgent).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)
				.Select(x=>x.FullName).ToList();
		}
		
		public static List<string> GetColorSources() {
			return AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes())
				.Where(x => typeof(IColorTarget).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)
				.Select(x=>x.Name).ToList();
		}
	}
}