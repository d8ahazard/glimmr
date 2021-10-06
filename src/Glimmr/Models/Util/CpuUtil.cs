#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Glimmr.Enums;
using Serilog;

#endregion

namespace Glimmr.Models.Util {
	public static class CpuUtil {
		private const StringComparison Sc = StringComparison.InvariantCulture;
		private static float _loadAvg1;
		private static float _loadAvg15;
		private static float _loadAvg5;
		private static double _tempAverage;
		private static double _tempMax;
		private static double _tempMin = 10000.0d;
		private static string[]? _throttledState;
		private static string? _upTime;

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

		public static async Task<CpuData> GetStats() {
			_throttledState = await GetThrottledState();
			var temp = await GetTemperature();
			await GetProcessAverage();
			TemperatureSetMinMax(temp);
			var cd = new CpuData {
				TempCurrent = temp,
				TempMin = (float) _tempMin,
				TempMax = (float) _tempMax,
				TempAvg = (float) _tempAverage,
				Uptime = _upTime,
				LoadAvg1 = _loadAvg1,
				LoadAvg5 = _loadAvg5,
				LoadAvg15 = _loadAvg15,
				ThrottledState = _throttledState
			};
			return cd;
		}

		private static async Task<float> GetTemperature() {
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
				temp = (float) Math.Round(temp * 1.8f + 32);
			}

			return temp;
		}

		private static async Task<string[]> GetThrottledState() {
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

		private static async Task GetProcessAverage() {
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
			if (float.TryParse(loadAverages[0], out var la1)) _loadAvg1 = la1;
			if (float.TryParse(loadAverages[1], out var la5)) _loadAvg5 = la5;
			if (float.TryParse(loadAverages[2], out var la15)) _loadAvg15 = la15;
			_upTime = processResult.Split(",")[1].Trim();
		}


		private static void TemperatureSetMinMax(float temperature) {
			try {
				_tempAverage = temperature;

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