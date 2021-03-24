using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DirectShowLib;
using Emgu.CV;
using Newtonsoft.Json;
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
			Log.Debug("Full path is " + fullPath);
			if (!Directory.Exists(fullPath)) {
				try {
					Directory.CreateDirectory(fullPath);
					Log.Debug("Returning: " + fullPath);
					return fullPath;
				} catch (Exception e) {
					Log.Warning("Exception creating userdata dir: " + e.Message);
					return string.Empty;
				}
			}

			if (Directory.Exists(fullPath)) return fullPath;
			return String.Empty;
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
				} catch (Exception) {
					// Ignored
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
						Log.Debug($"Width, height of {i}: {w}, {h}");
						
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
					CreateNoWindow = true,
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


		private static Dictionary<int,string> ListUsbWindows() {
			var cams = new Dictionary<int, string>();

			try {
				var devices = new List<DsDevice>(DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice));
				var idx = 0;
				foreach (var device in devices) {
					Log.Debug("Adding cap dev: " + JsonConvert.SerializeObject(device));
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