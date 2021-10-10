#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Serilog;

#endregion

namespace Glimmr.Models.Util {
	public static class CpuUtil {
		private static float _tempMax;
		private static float _tempMin = float.MinValue;
		private static string[]? _throttledState;

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
			var mon = new CpuMonitor();
			var cd = mon.Monitor();
			_throttledState = await GetThrottledState();
			cd.ThrottledState = _throttledState;
			TemperatureSetMinMax(cd.CpuTemp);
			cd.TempMin = _tempMin;
			cd.TempMax = _tempMax;
			cd.Uptime = Environment.TickCount;
			return cd;
		}

		private static async Task<string[]> GetThrottledState() {
			if (!OperatingSystem.IsWindows()) {
				return await GetThrottledStateLinux();
			}

			return Array.Empty<string>();
		}
		
		private static async Task<string[]> GetThrottledStateLinux() {
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