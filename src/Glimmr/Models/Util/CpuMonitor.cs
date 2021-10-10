using System;
using LibreHardwareMonitor.Hardware;
using Serilog;

namespace Glimmr.Models.Util {
	public class CpuMonitor : IVisitor {
		public void VisitComputer(IComputer computer) {
			computer.Traverse(this);
		}

		public void VisitHardware(IHardware hardware) {
			hardware.Update();
			foreach (IHardware subHardware in hardware.SubHardware) subHardware.Accept(this);
		}

		public void VisitSensor(ISensor sensor) {
		}

		public void VisitParameter(IParameter parameter) {
		}
		
		public StatData Monitor() {
			Computer computer = new() {
				IsCpuEnabled = true,
				IsStorageEnabled = true,
				IsNetworkEnabled = true,
				IsGpuEnabled = true,
				IsMemoryEnabled = true,
				IsMotherboardEnabled = true
			};
			var output = new StatData();
			float usedMemory = -1;
			float totalMemory = -1;
			computer.Open();
			computer.Accept(new CpuMonitor());
			foreach (IHardware hardware in computer.Hardware) {
				Log.Debug($"Hardware: {hardware.Name} - {hardware.HardwareType}");
				switch (hardware.HardwareType) {
					case HardwareType.Cpu:
						foreach (ISensor sensor in hardware.Sensors) {
							if (sensor.Name == "CPU Total") {
								output.CpuUsage = (int)(sensor.Value ?? 0);
								Log.Debug(sensor.Name + ":" + sensor.Value);
							}

							if (sensor.Name == "Core (Tctl/Tdie)") {
								output.CpuTemp = sensor.Value ?? 0;
								Log.Debug(sensor.Name + ":" + sensor.Value);
							}

							foreach (var sub in hardware.SubHardware) {
								Log.Debug("Sub: " + sub.Name);
								foreach (var subSensor in sub.Sensors) {
									Log.Debug($"SubSensor: {subSensor.Name}: {subSensor.Value}");
								}
							}
							Log.Debug(sensor.Name + ":" + sensor.Value);
						}
						break;
					case HardwareType.Memory:
						foreach (ISensor sensor in hardware.Sensors) {
							if (sensor.Name == "Memory") {
								output.MemoryUsage = (int) (sensor.Value ?? 0);
								Log.Debug(sensor.Name + ":" + sensor.Value);
							}
						}
						break;
					default:
						foreach (ISensor sensor in hardware.Sensors) {
							Log.Debug(sensor.Name + ":" + sensor.Value);
							foreach (var sub in hardware.SubHardware) {
								Log.Debug("Sub: " + sub.Name);
								foreach (var subSensor in sub.Sensors) {
									Log.Debug($"SubSensor: {subSensor.Name}: {subSensor.Value}");
								}
							}
							Log.Debug(sensor.Name + ":" + sensor.Value);
						}

						break;
				}
			}

			if (Math.Abs(usedMemory - -1f) > float.MinValue && Math.Abs(totalMemory - -1f) > float.MinValue) {
				output.MemoryUsage = usedMemory / totalMemory;
			}
			computer.Close();
			return output;
		}
	}
}

