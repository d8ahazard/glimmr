#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Glimmr.Enums;
using Serilog;

#endregion

namespace Glimmr.Models.Util {
	public static class CpuUtil {
		private static float _tempMax;
		private static float _tempMin = float.MinValue;
		private static string[]? _throttledState;
		private const StringComparison Sc = StringComparison.InvariantCulture;

		private static readonly string[] StringTable = {
			"Soft Temperature Limit has occurred", //19
			"Throttling has occurred",
			"Arm frequency capping has occurred",
			"Under volt has occurred",
			"", //15
			"",
			"",
			"",
			"",
			"",
			"",
			"",
			"",
			"",
			"",
			"", //3
			"Soft temperature limit active",
			"Currently throttled",
			"ARM frequency capped",
			"Under-voltage detected"
		};

		public static async Task<StatData> GetStats() {
			var cd = new StatData();
			if (OperatingSystem.IsWindows()) {
				cd = CpuMonitor.Monitor();
			} else {
				cd.CpuUsage = GetProcessAverage().Result;
				cd.CpuTemp = GetTemperature().Result;
				cd.MemoryUsage = GetMemoryUsage();
				_throttledState = await GetThrottledState();
				cd.ThrottledState = _throttledState;	
			}
			
			TemperatureSetMinMax(cd.CpuTemp);
			cd.TempMin = _tempMin;
			cd.TempMax = _tempMax;
			return cd;
		}

		private static async Task<string[]> GetThrottledState() {
			if (SystemUtil.IsRaspberryPi()) {
				return await GetThrottledStatePi();
			}

			return Array.Empty<string>();
		}

		private static async Task<float> GetTemperature() {
			if (SystemUtil.IsRaspberryPi()) {
				return await GetTempPi();
			}

			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
				return await GetTempOsx();
			}

			return await GetTempLinux();
		}
		
		private static async Task<float> GetTempOsx() {
			// bash command / opt / vc / bin / vc gen cmd measure_temp
			var temp = 0f;
			try {
				var process = new Process {
					StartInfo = new ProcessStartInfo {
						FileName = "sysctl",
						Arguments = "machdep.xcpm.cpu_thermal_level",
						RedirectStandardOutput = true,
						UseShellExecute = false,
						CreateNoWindow = true
					}
				};
				process.Start();
				while (!process.StandardOutput.EndOfStream) {
					var res = await process.StandardOutput.ReadLineAsync();
					if (res != null && res.Contains("machdep.xcpm.cpu_thermal_level")) {
						var p1 = res.Replace("machdep.xcpm.cpu_thermal_level: ", "");
						if (!float.TryParse(p1, out temp)) {
							Log.Warning("Unable to parse line: " + res);
						}
					}
				}

				process.Dispose();
			} catch (Exception e) {
				Log.Warning("Exception: " + e.Message);
			}

			var sd = DataUtil.GetSystemData();
			if (sd.Units == DeviceUnits.Imperial) {
				temp = (float)Math.Round(temp * 1.8f + 32);
			}
			
			return temp;
		}
		
		private static async Task<float> GetTempLinux() {
			// bash command / opt / vc / bin / vc gen cmd measure_temp
			var temp = 0f;
			try {
				var process = new Process {
					StartInfo = new ProcessStartInfo {
						FileName = "/bin/bash",
						Arguments = "-c \"sensors\"",
						RedirectStandardOutput = true,
						UseShellExecute = false,
						CreateNoWindow = true
					}
				};
				process.Start();
				while (!process.StandardOutput.EndOfStream) {
					var res = await process.StandardOutput.ReadLineAsync();
					if (res != null && res.Contains("temp")) {
						var p1 = res.Split("+")[1];
						if (p1.Contains(" ")) {
							p1 = p1.Split(" ")[0];
						}
						if (!float.TryParse(p1, out temp)) {
							Log.Warning("Unable to parse line: " + res);
						}
					}
				}

				process.Dispose();
			} catch (Exception e) {
				Log.Warning("Exception: " + e.Message);
			}

			var sd = DataUtil.GetSystemData();
			if (sd.Units == DeviceUnits.Imperial) {
				temp = (float)Math.Round(temp * 1.8f + 32);
			}
			
			return temp;
		}

		private static async Task<float> GetTempPi() {
			// bash command / opt / vc / bin / vc gen cmd measure_temp
			var process = new Process {
				StartInfo = new ProcessStartInfo {
					FileName = "/bin/bash",
					Arguments = "-c \"/opt/vc/bin/vcgencmd measure_temp\"",
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true
				}
			};
			process.Start();
			var result = await process.StandardOutput.ReadToEndAsync();
			await process.WaitForExitAsync();
			process.Dispose();
			var res = result.Split("=")[1].Split("'")[0];
			var temp = float.TryParse(res, out var temperature) ? temperature : 0.0f;
			var sd = DataUtil.GetSystemData();
			if (sd.Units == DeviceUnits.Imperial) {
				temp = (float)Math.Round(temp * 1.8f + 32);
			}
			
			return temp;
		}

		private static int GetMemoryUsage() {
			return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? GetMemoryUsageOsx() : GetMemoryUsageLinux();
		}
		
		private static int GetMemoryUsageLinux() {
			
			var process = new Process {
				StartInfo = new ProcessStartInfo {
					FileName = "vmstat",
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true,
					Arguments = "-s"
				}
			};
			process.Start();
			var used = -1f;
			var total = -1f;
			while (!process.StandardOutput.EndOfStream) {
				var data = process.StandardOutput.ReadLineAsync().Result;
				if (data == null) continue;
				if (data.Contains("total memory")) {
					var str = data.Replace(" used memory", "");
					if (float.TryParse(str.Split(" ")[0], out var foo)) {
						used = foo;
					}
				}
				if (data.Contains("used memory")) {
					var str = data.Replace(" total memory", "");
					if (float.TryParse(str.Split(" ")[0], out var foo)) {
						total = foo;
					}
				}
			}

			if (Math.Abs(used - -1) > float.MinValue && Math.Abs(total - -1f) > float.MinValue) {
				return (int)(used / total);
			}
			process.Dispose();
			return 0;
		}

		private static int GetMemoryUsageOsx() {
			var process = new Process {
				StartInfo = new ProcessStartInfo {
					FileName = "vm_stat",
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true
				}
			};
			process.Start();
			var free = 0;
			var active = 0;
			var spec = 0;
			var throttled = 0;
			var wired = 0;
			while (!process.StandardOutput.EndOfStream) {
				var data = process.StandardOutput.ReadLineAsync().Result;
				if (data == null) continue;

				string line;
				if (data.Contains("Pages free")) {
					line = data.Split(":")[1].Replace(".", "").Trim().TrimEnd();
					if (int.TryParse(line, out var foo)) {
						free = foo;
					}
				}
				if (data.Contains("Pages active")) {
					line = data.Split(":")[1].Replace(".", "").Trim().TrimEnd();
					if (int.TryParse(line, out var foo)) {
						active = foo;
					}
				}
				if (data.Contains("Pages speculative")) {
					line = data.Split(":")[1].Replace(".", "").Trim().TrimEnd();
					if (int.TryParse(line, out var foo)) {
						spec = foo;
					}
				}
				if (data.Contains("Pages throttled")) {
					line = data.Split(":")[1].Replace(".", "").Trim().TrimEnd();
					if (int.TryParse(line, out var foo)) {
						throttled = foo;
					}
				}
				if (data.Contains("Pages wired down")) {
					line = data.Split(":")[1].Replace(".", "").Trim().TrimEnd();
					if (int.TryParse(line, out var foo)) {
						wired = foo;
					}
				}
			}

			var total = free + active + spec + throttled + wired;
			process.Dispose();
			return total == 0 ? 0 : active / total;
		}
		
		private static async Task<int> GetProcessAverage() {
			var process = new Process {
				StartInfo = new ProcessStartInfo {
					FileName = "/usr/bin/uptime",
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true
				}
			};
			process.Start();
			var processResult = (await process.StandardOutput.ReadToEndAsync()).Trim();
			await process.WaitForExitAsync();
			process.Dispose();
			var loadAverages = processResult[(processResult.IndexOf("average: ", Sc) + 9)..].Split(',');
			if (float.TryParse(loadAverages[0], out var la1)) return (int) la1;
			return 0;
		}
		
		private static async Task<string[]> GetThrottledStatePi() {
			var process = new Process {
				StartInfo = new ProcessStartInfo {
					FileName = "/bin/bash",
					Arguments = "-c \"/opt/vc/bin/vcgencmd get_throttled\"",
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true
				}
			};
			process.Start();
			var result = await process.StandardOutput.ReadToEndAsync();
			await process.WaitForExitAsync();
			process.Dispose();
			result = result.Trim();
			var split = result.Split("x")[1];
			var bin = Hex2Bin(split);
			var messages = new List<string>();
			for (var i = 0; i < bin.Length; i++) {
				if (bin[i].ToString(CultureInfo.InvariantCulture) == "1") {
					messages.Add(StringTable[i]);
				}
			}

			return messages.ToArray();
		}
		
		private static string Hex2Bin(string hexString) {
			var output = string.Join(string.Empty,
				hexString.Select(
					c => Convert.ToString(Convert.ToInt32(c.ToString(CultureInfo.InvariantCulture), 16), 2)
						.PadLeft(4, '0')
				)
			);
			return output;
		}

		private static void TemperatureSetMinMax(float temperature) {
			try {
				if (_tempMin > temperature) {
					_tempMin = temperature;
				}

				if (_tempMax < temperature) {
					_tempMax = temperature;
				}
			} catch (Exception ex) {
				Log.Warning("Got me some kind of exception: " + ex.Message);
			}
		}
	}
	
}