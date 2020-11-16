using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace Glimmr.Models.Util {
	public static class CpuUtil {
		private static double _tempMin = 10000.0d;
		private static double _tempMax;
		private static double _tempAverage;
		private static float _loadAvg1;
		private static float _loadAvg5;
		private static float _loadAvg15;
		private static string _upTime;
		private static string[] _throttledState;
		private const StringComparison _sc = StringComparison.InvariantCulture;
		
		private static readonly string[] StringTable = {
			"Soft Temperature Limit has occurred", //19
			"Throttling has occurred",
			"Arm frequency capping has occurred",
			"Under volting has occurred",
			"",//15
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
			"",//3
			"Soft temperature limit active",
			"Currently throttled",
			"ARM frequency capped",
			"Under-voltage detected"
		};

		public static CpuData GetStats() {
			_throttledState = GetThrottledState();
			var temp = GetTemperature();
			GetProcessAverage();
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

		private static float GetTemperature() {
			// bash command / opt / vc / bin / vcgencmd measure_temp
			var process = new Process {
				StartInfo = new ProcessStartInfo {
					FileName = "/bin/bash",
					Arguments = "-c \"/opt/vc/bin/vcgencmd measure_temp\"",
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true,
				}
			};
			process.Start();
			var result = process.StandardOutput.ReadToEnd();
			process.WaitForExit();
			process.Dispose();
			var res = result.Split("=")[1].Split("'")[0];
			return float.TryParse(res, out var temperature) ? temperature : 0.0f;
		}

		private static string[] GetThrottledState() {
			var process = new Process {
				StartInfo = new ProcessStartInfo {
					FileName = "/bin/bash",
					Arguments = "-c \"/opt/vc/bin/vcgencmd get_throttled\"",
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true,
				}
			};
			process.Start();
			var result = process.StandardOutput.ReadToEnd();
			process.WaitForExit();
			process.Dispose();
			result = result.Trim();
			var split = result.Split("x")[1];
			var bin = Hex2Bin(split);
			var messages = new List<String>();
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
					c => Convert.ToString(Convert.ToInt32(c.ToString(CultureInfo.InvariantCulture), 16), 2).PadLeft(4, '0')
				)
			);
			return output;
		}

		private static void GetProcessAverage() {
			var process = new Process {
				StartInfo = new ProcessStartInfo {
					FileName = "/usr/bin/uptime",
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true,
				}
			};
			process.Start();
			var processResult = process.StandardOutput.ReadToEnd().Trim();
			process.WaitForExit();
			process.Dispose();
			var loadAverages = processResult.Substring(processResult.IndexOf("average: ", _sc) + 9).Split(',');
			float.TryParse(loadAverages[0], out _loadAvg1);
			float.TryParse(loadAverages[1], out _loadAvg5);
			float.TryParse(loadAverages[2], out _loadAvg15);
			_upTime = processResult.Split(",")[1].Trim();
		}


		
		
		private static void TemperatureSetMinMax(float temperature) {
			try {
				if (_tempAverage == 0.0)
					_tempAverage = temperature;
				else
					_tempAverage = temperature;
				

				if (_tempMin > temperature)
					_tempMin = temperature;

				if (_tempMax < temperature)
					_tempMax = temperature;
			} catch (Exception ex) {
				LogUtil.Write("Got me some kind of exception: " + ex.Message);
			}
		}
	}
}