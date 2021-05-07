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
using SharpDX.Text;

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

		public static bool IsOnline(string target) {
			var pingSender = new Ping ();

			// Create a buffer of 32 bytes of data to be transmitted.
			const string data = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
			const int timeout = 1;

			var buffer = Encoding.ASCII.GetBytes (data);
			var options = new PingOptions (64, true);

			// Send the request.
			var reply = pingSender.Send(target, timeout, buffer, options);
			var isOnline = reply != null && reply.Status == IPStatus.Success;
			if (!isOnline) Log.Information("Device " + target + " is offline.");
			return isOnline;
		}

		public static void Update() {
			Log.Debug("Updating");
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
				Process.Start("/bin/bash","/etc/init.d/update_pi.sh");
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
				userDir = "/etc/";
			}

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) Log.Debug("Platform is Windows");
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) Log.Debug("Platform is OSX");
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