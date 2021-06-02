using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DirectShowLib;
using Emgu.CV;
using Serilog;

namespace Glimmr.Models.Util {
	public static class SystemUtil {
		public static void Reboot() {
			Log.Debug("Rebooting");
			Process.Start("shutdown", RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "/r /t 0" : "-r now");
		}

		public static bool IsOnline(string target) {
			if (string.IsNullOrEmpty(target)) {
				return false;
			}

			if (target == "127.0.0.1" || target == "localhost") {
				return true;
			}

			var pingable = false;
			Ping pinger = null;

			try {
				pinger = new Ping();
				var reply = pinger.Send(target);
				if (reply != null) {
					pingable = reply.Status == IPStatus.Success;
				}
			} catch (PingException) {
				//ignore
			} finally {
				pinger?.Dispose();
			}

			return pingable;
		}

		public static bool IsRaspberryPi() {
			var path = "/usr/bin/raspi-config";
			if (File.Exists(path)) {
				Log.Debug("Raspi-config found at " + path);
				return true;
			}

			Log.Debug("Raspi-config was NOT found at " + path);
			return false;
		}

		public static void Update() {
			Log.Debug("Updating");
			DataUtil.ExportSettings();
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				Process.Start("../script/update_win.bat");
			} else {
				Process.Start("/bin/bash",
					IsRaspberryPi()
						? "/home/glimmrtv/glimmr/script/update_pi.sh"
						: "/home/glimmrtv/glimmr/script/update_linux.sh");
			}
		}

		public static void Restart() {
			Log.Debug("Restarting glimmr.");
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				var path = AppDomain.CurrentDomain.BaseDirectory;
				path = Path.Join(path, "..", "script", "restart_win.ps1");
				Process.Start("powershell", path);
			} else {
				Process.Start("service", "glimmr restart");
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
			var userDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "C:\\ProgramData\\" : "/etc/";

			var fullPath = Path.Combine(userDir, "Glimmr");
			if (!Directory.Exists(fullPath)) {
				try {
					Directory.CreateDirectory(fullPath);
					return fullPath;
				} catch (Exception e) {
					Log.Warning("Exception creating userdata dir: " + e.Message);
					return string.Empty;
				}
			}

			if (Directory.Exists(fullPath)) {
				return fullPath;
			}

			return string.Empty;
		}

		public static List<string> GetClasses<T>() {
			var output = new List<string>();
			foreach (var ad in AppDomain.CurrentDomain.GetAssemblies()) {
				try {
					var types = ad.GetTypes();
					foreach (var type in types) {
						if (typeof(T).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract) {
							output.Add(type.FullName);
						}
					}
				} catch (Exception e) {
					Log.Warning("Exception listing types: " + e.Message);
				}
			}

			return output;
		}

		private static Dictionary<int, string> ListUsbLinux() {
			var i = 0;
			var output = new Dictionary<int, string>();
			while (i < 10) {
				try {
					// Check if video stream is available.
					var v = new VideoCapture(i); // Will crash if not available, hence try/catch.
					var w = v.Width;
					var h = v.Height;

					if (w != 0 && h != 0) {
						output[i] = GetDeviceName(i).Result;
					}

					v.Dispose();
				} catch (Exception e) {
					Log.Debug("Exception with cam " + i + ": " + e);
				}

				i++;
			}

			return output;
		}

		private static async Task<string> GetDeviceName(int index) {
			var process = new Process {
				StartInfo = new ProcessStartInfo {
					FileName = "/bin/bash",
					Arguments = $"-c \"cat /sys/class/video4linux/video{index}/name\"",
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true
				}
			};
			process.Start();
			var result = await process.StandardOutput.ReadToEndAsync();
			await process.WaitForExitAsync();
			process.Dispose();
			return result;
		}

		public static Dictionary<int, string> ListUsb() {
			return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ListUsbWindows() : ListUsbLinux();
		}


		private static Dictionary<int, string> ListUsbWindows() {
			var cams = new Dictionary<int, string>();

			try {
				var devices = new List<DsDevice>(DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice));
				var idx = 0;
				foreach (var device in devices) {
					cams[idx] = device.Name;
					idx++;
				}
			} catch (Exception e) {
				Log.Warning("Exception fetching USB devices: " + e.Message);
			}


			return cams;
		}
	}
}