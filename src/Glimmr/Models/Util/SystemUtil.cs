#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DirectShowLib;
using Emgu.CV;
using Glimmr.Hubs;
using Glimmr.Models.Constant;
using Microsoft.AspNetCore.SignalR;
using Serilog;

#endregion

namespace Glimmr.Models.Util;

public static class SystemUtil {
	private static DateTime? LastUpdate;

	public static void Reboot() {
		Log.Debug("Rebooting");
		if (IsRaspberryPi()) {
			var appDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
			var cmd = Path.Join(appDir, "reboot.sh");
			Log.Debug("Reboot command should be: " + cmd);
			Process.Start("/bin/bash", cmd);
		} else {
			Process.Start("shutdown", RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "/r /t 0" : "-r now");
		}
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

	public static bool IsDocker() {
		return File.Exists("/docker_install.sh");
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
		return output.ToLower().Contains("raspbian") || File.Exists("/usr/bin/raspi-config");
	}

	public static void Update(IHubContext<SocketServer> hubContext) {
		var updateNeeded = false;
		var now = DateTime.Now;
		if (LastUpdate != null) {
			var last = (DateTime)LastUpdate;
			var diff = last - now;
			var diffInSeconds = Math.Abs(diff.TotalSeconds);
			updateNeeded = diffInSeconds <= 60;
			if (updateNeeded) {
				hubContext.Clients.All.SendAsync("toast", "Updating", "Forcing update.");
				Log.Information($"Last update attempt was {diffInSeconds} seconds ago, forcing update.");
			}
		}

		LastUpdate = DateTime.Now;
		var version = Version.Parse("0.0.0");
		var ver = Assembly.GetEntryAssembly()?.GetName().Version;
		if (ver != null) {
			version = ver;
		}

		if (!updateNeeded) {
			try {
				using var handler = new HttpClientHandler();
				handler.UseDefaultCredentials = true;
				using var client = new HttpClient(handler);
				client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Glimmr",
					"1.2.6")); // set your own values here
				var cj = client.GetFromJsonAsync<GithubRelease>(GlimmrConstants.relUrl).Result;
				if (cj != null) {
					Log.Debug("Release fetched...");
					var tag = cj.tag_name;
					var newVersion = Version.Parse(tag);
					Log.Debug("Tag is " + tag + " versus " + version);
					updateNeeded = newVersion.CompareTo(version) > 0;
					if (updateNeeded) {
						hubContext.Clients.All.SendAsync("toast", "Updating",
							"Updating from	" + version + " to " + newVersion + ".");
					}
				} else {
					Log.Debug("Can't get cj there...");
				}
			} catch (Exception e) {
				Log.Warning("Exception updating: " + e.Message);
			}
		}

		if (!updateNeeded) {
			hubContext.Clients.All.SendAsync("toast", "Up-to-date",
				$"Software version {version} is the latest available.");
			Log.Debug("No updates are required at this time.");
			return;
		}

		Log.Debug("Updating...");
		Log.Information("Backing up current settings...");
		DataUtil.BackupDb();

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
			var appDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
			var cmd = Path.Join(appDir, "update_win.ps1");
			UpdateUpdater("update_win.ps1", cmd);
			Ps("update_win.ps1");
		}

		if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
			var appDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
			var cmd = Path.Join(appDir, "update_osx.sh");
			UpdateUpdater("update_osx.sh", cmd);
			Log.Debug("Update command should be: " + cmd);
			Process.Start("/bin/bash", cmd);
		}

		if (!IsRaspberryPi() && !RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
			return;
		}

		{
			var appDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
			var cmd = Path.Join(appDir, "update_linux.sh");
			UpdateUpdater("update_linux.sh", cmd);
			Log.Debug("Update command should be: " + cmd);
			Process.Start("/bin/bash", cmd);
		}
	}

	private static void UpdateUpdater(string file, string target) {
		var url = $"https://raw.githubusercontent.com/d8ahazard/glimmr/master/src/Glimmr/script/{file}";
		using var client = new HttpClient();
		var res = client.GetStringAsync(url).Result;
		try {
			File.WriteAllText(target, res);
		} catch (Exception e) {
			Log.Debug("Exception updating update script: " + e.Message);
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
			userDir = "/Library/Application Support/";
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
				var types = ad.GetTypes() ?? throw new ArgumentNullException(ad.ToString());
				output.AddRange(from type in types
					where typeof(T).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract
					where type.FullName != null
					select type.FullName);
			} catch (Exception e) {
				Log.Warning("Exception listing types: " + e.Message);
			}
		}

		return output;
	}

	private static async Task<string> GetVideoName(int index) {
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
			return await GetVideoNameLinux(index);
		}

		return await GetVideoNameOsx(index);
	}

	private static async Task<string> GetVideoNameLinux(int index) {
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

	private static Task<string> GetVideoNameOsx(int index) {
		return Task.FromResult($"Device {index}");
	}

	public static Dictionary<int, string> ListUsb() {
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
			return ListUsbWindows();
		}

		if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
			return ListUsbOsx();
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

				if (usb == i || (w != 0 && h != 0)) {
					output[i] = GetVideoNameLinux(i).Result;
				}

				v.Dispose();
			} catch (Exception) {
				// Ignored
			}

			i++;
		}

		return output;
	}

	private static Dictionary<int, string> ListUsbOsx() {
		var sd = DataUtil.GetSystemData();
		var usb = sd.UsbSelection;
		var i = 0;
		var output = new Dictionary<int, string>();
		while (i < 10) {
			var res = CheckVideo(i, usb, VideoCapture.API.Any);
			if (string.IsNullOrEmpty(res)) {
				res = CheckVideo(i, usb, VideoCapture.API.DShow);
			}

			if (string.IsNullOrEmpty(res)) {
				res = CheckVideo(i, usb, VideoCapture.API.QT);
			}

			if (string.IsNullOrEmpty(res)) {
				res = CheckVideo(i, usb, VideoCapture.API.AVFoundation);
			}

			if (!string.IsNullOrEmpty(res)) {
				output[i] = res;
			}

			i++;
		}

		return output;
	}

	private static string? CheckVideo(int index, int selection, VideoCapture.API api) {
		string? result = null;
		try {
			var v = new VideoCapture(index, api); // Will crash if not available, hence try/catch.
			var w = v.Width;
			var h = v.Height;

			if (selection == index || (w != 0 && h != 0)) {
				result = GetVideoName(index).Result;
			}

			v.Dispose();
		} catch (Exception e) {
			Log.Debug("Exception checking video: " + e.Message);
		}

		return result;
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