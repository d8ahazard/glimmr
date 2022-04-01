#region

using System.Runtime.InteropServices;
using Glimmr.Models.Data;
using LibreHardwareMonitor.Hardware;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.Util;

public class CpuMonitor : IVisitor {
	public void VisitComputer(IComputer computer) {
		computer.Traverse(this);
	}

	public void VisitHardware(IHardware hardware) {
		hardware.Update();
		foreach (var subHardware in hardware.SubHardware) {
			subHardware.Accept(this);
		}
	}

	public void VisitSensor(ISensor sensor) { }

	public void VisitParameter(IParameter parameter) { }

	public static StatData Monitor() {
		Computer computer = new() {
			IsCpuEnabled = true,
			IsStorageEnabled = true,
			IsNetworkEnabled = true,
			IsGpuEnabled = true,
			IsMemoryEnabled = true,
			IsMotherboardEnabled = true
		};
		var output = new StatData();
		computer.Open();
		computer.Accept(new CpuMonitor());
		foreach (var hardware in computer.Hardware) {
			switch (hardware.HardwareType) {
				case HardwareType.Cpu:
					foreach (var sensor in hardware.Sensors) {
						switch (sensor.Name) {
							case "CPU Total":
								output.CpuUsage = (int)(sensor.Value ?? 0);
								break;
							case "Core (Tctl/Tdie)":
							case "Core (Tctl)":
							case "Core (Tdie)":
								output.CpuTemp = (int)(sensor.Value ?? 0);
								break;
						}
					}

					break;
				case HardwareType.Memory:
					foreach (var sensor in hardware.Sensors) {
						if (sensor.Name == "Memory") {
							output.MemoryUsage = (int)(sensor.Value ?? 0);
						}
					}

					break;
			}
		}

		computer.Close();
		return output;
	}

	public static int GetMemoryWindows(bool returnTotal = false) {
		ulong installedMemory = 0;
		ulong usedMemory = 0;
		var memStatus = new MemState();
		if (!GlobalMemoryStatusEx(memStatus)) {
			return returnTotal ? (int)installedMemory : (int)usedMemory;
		}

		installedMemory = memStatus.ullTotalPhys;
		usedMemory = memStatus.dwMemoryLoad;
		return returnTotal ? (int)installedMemory : (int)usedMemory;
	}

	[return: MarshalAs(UnmanagedType.Bool)]
	[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern bool GlobalMemoryStatusEx([In] [Out] MemState lpBuffer);

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	private class MemState {
		[JsonProperty] public uint dwLength;

		[JsonProperty] public uint dwMemoryLoad;

		[JsonProperty] public ulong ullAvailExtendedVirtual;

		[JsonProperty] public ulong ullAvailPageFile;

		[JsonProperty] public ulong ullAvailPhys;

		[JsonProperty] public ulong ullAvailVirtual;

		[JsonProperty] public ulong ullTotalPageFile;

		[JsonProperty] public ulong ullTotalPhys;

		[JsonProperty] public ulong ullTotalVirtual;

		public MemState() {
			dwLength = (uint)Marshal.SizeOf(typeof(MemState));
		}
	}
}