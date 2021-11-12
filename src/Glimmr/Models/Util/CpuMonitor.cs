#region

using LibreHardwareMonitor.Hardware;

#endregion

namespace Glimmr.Models.Util {
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

		public void VisitSensor(ISensor sensor) {
		}

		public void VisitParameter(IParameter parameter) {
		}

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
			foreach (IHardware hardware in computer.Hardware) {
				switch (hardware.HardwareType) {
					case HardwareType.Cpu:
						foreach (ISensor sensor in hardware.Sensors) {
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
						foreach (ISensor sensor in hardware.Sensors) {
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
	}
}