#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DirectShowLib;
using Emgu.CV;
using Serilog;

#endregion

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

			if (target is "127.0.0.1" or "localhost") {
				return true;
			}

			var pingable = false;
			Ping? ping = null;

			try {
				ping = new Ping();
				var reply = ping.Send(target);
				pingable = reply.Status == IPStatus.Success;
			} catch (PingException) {
				//ignore
			} finally {
				ping?.Dispose();
			}

			return pingable;
		}

		public static bool IsRaspberryPi() {
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
				return false;
			}

			Process process = new() {
				StartInfo = {
					FileName = "cat",
					Arguments = "/etc/os-release",
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true
				}
			};
			process.Start();
			var output = process.StandardOutput.ReadToEnd();
			process.WaitForExit();
			return output.ToLower().Contains("raspbian");
		}

		public static string GetBranch() {
			var branch = "master";
			var assembly = Assembly.GetEntryAssembly();
			if (assembly == null) {
				return branch;
			}

			var attrib = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
			if (attrib == null) {
				return branch;
			}

			var ver = attrib.InformationalVersion;
			if (!ver.Contains("dev")) {
				return branch;
			}

			Log.Information("We should be using the dev branch to update.");
			branch = "dev";

			return branch;
		}

		public static void Update() {
			Log.Debug("Updating...");
			Log.Information("Backing up current settings...");
			DataUtil.BackupDb();
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				Ps("update_win");
			}

			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
				var appDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
				var cmd = Path.Join(appDir, "update_osx.sh");
				Log.Debug("Update command should be: " + cmd);
				Process.Start("/bin/bash", cmd);	
			}

			if (IsRaspberryPi() || RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
				var appDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
				var cmd = Path.Join(appDir, "update_linux.sh");
				Log.Debug("Update command should be: " + cmd);
				Process.Start("/bin/bash", cmd);
			}
		}

		private static void Ps(string filename) {
			var path = AppDomain.CurrentDomain.BaseDirectory;
			path = Path.Join(path, $"{filename}.ps1");
			if (File.Exists(path)) {
				Process.Start("powershell.exe",
					$"-NoProfile -ExecutionPolicy unrestricted -File \"{path}\"");
			} else {
				Log.Warning("Unable to find script: " + filename);
			}
		}

		public static void Restart() {
			Log.Debug("Restarting glimmr.");
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				Ps("restart_win");
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
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
				userDir = "~/Library/Application Support/";
			}

			var fullPath = Path.Combine(userDir, "Glimmr");
			if (Directory.Exists(fullPath)) {
				return Directory.Exists(fullPath) ? fullPath : string.Empty;
			}

			try {
				Directory.CreateDirectory(fullPath);
				return fullPath;
			} catch (Exception e) {
				Log.Warning("Exception creating userdata dir: " + e.Message);
				return string.Empty;
			}
		}

		public static List<string> GetClasses<T>() {
			var output = new List<string>();
			foreach (var ad in AppDomain.CurrentDomain.GetAssemblies()) {
				try {
					var types = ad.GetTypes();
					foreach (var type in types) {
						if (!typeof(T).IsAssignableFrom(type) || type.IsInterface || type.IsAbstract) {
							continue;
						}

						if (type.FullName != null) {
							output.Add(type.FullName);
						}
					}
				} catch (Exception e) {
					Log.Warning("Exception listing types: " + e.Message);
				}
			}

			return output;
		}

		public static async Task<string[]> ReadLogLines(int len = 500) {
			var dt = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
			var logPath = $"/var/log/glimmr/glimmr{dt}.log";
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				var userPath = GetUserDir();
				var logDir = Path.Combine(userPath, "log");
				if (!Directory.Exists(logDir)) {
					Directory.CreateDirectory(logDir);
				}

				logPath = Path.Combine(userPath, "log", $"glimmr{dt}.log");
			}

			var result = await File.ReadAllLinesAsync(logPath);
			if (result.Length > len) {
				result = result.Skip(Math.Max(0, result.Length - len)).ToArray();
			}

			return result;
		}

		public static bool IsFileReady(string filename) {
			// If the file can be opened for exclusive access it means that the file
			// is no longer locked by another process.
			try {
				using FileStream inputStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.None);
				return inputStream.Length > 0;
			} catch (Exception) {
				return false;
			}
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
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				return ListUsbWindows();
			}

			try {
				return ListUsbLinux();
			} catch (Exception) {
				// Ignored
			}

			return new Dictionary<int, string>();
		}

		private static Dictionary<int, string> ListUsbLinux() {
			var sd = DataUtil.GetSystemData();
			var usb = sd.UsbSelection;
			var i = 0;
			var output = new Dictionary<int, string>();
			while (i < 10) {
				try {
					// Check if video stream is available.
					var v = new VideoCapture(i); // Will crash if not available, hence try/catch.
					var w = v.Width;
					var h = v.Height;

					if (usb == i || w != 0 && h != 0) {
						output[i] = GetDeviceName(i).Result;
					}

					v.Dispose();
				} catch (Exception) {
					// Ignored
				}

				i++;
			}

			return output;
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