using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Serilog;

namespace Glimmr.Models.ColorSource.Audio; 

public static class AudioInfo {
	public static int ChannelFrequency(int Device) {
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
			return ChannelFrequencyLinux(Device);
		}

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
			return ChannelFrequencyWindows(Device);
		}

		if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
			return ChannelFrequencyOSX(Device);
		}

		return -1;
	}

	private static int ChannelFrequencyOSX(int Device) {
		var freq = -1;
		return freq;
	}

	private static int ChannelFrequencyWindows(int Device) {
		var freq = -1;
		var oemGuid = "E4870E26-3CC5-4CD2-BA46-CA0A9A70ED04".ToLower();
		var oem = new Guid(oemGuid);
		Log.Debug("Getting windows device?");
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return freq;
		
		return freq;
	}

	private static int ChannelFrequencyLinux(int idx) {
		var freq = -1;
		Log.Debug("Device index is " + idx);
		var file = $"/proc/asound/card{idx}/stream0";
		if (!File.Exists(file)) {
			return freq;
		}

		Log.Debug("We're going in!");
		var nfo = File.ReadAllLines(file);
		if ((from line in nfo where line.Contains("Rates") select line.Split("Rates:")[1].Split(" ")[1]).Any(num => int.TryParse(num, out freq))) {
			return freq;
		}
		
		return freq;
	}
}