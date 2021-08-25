#region

using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Services;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Adalight {
	public class AdalightDiscovery : ColorDiscovery, IColorDiscovery {
		public virtual string DeviceTag { get; set; } = "Adalight";

		public AdalightDiscovery(ColorService cs) : base(cs) {
		}

		public async Task Discover(CancellationToken ct, int timeout) {
			Log.Debug("Adalight: Discovery started.");
			var discoTask = Task.Run(() => {
				try {
					var devs = FindDevices();
					Log.Debug("Found" + devs.Count + " devices.");
					foreach (var dev in devs) {
						var count = dev.Value.Key;
						var bri = dev.Value.Value;
						try {
							Log.Debug("Trying: " + dev.Key);
							var ac = new AdalightNet.Adalight(dev.Key, 20);
							ac.Connect();
							if (ac.Connected) {
								Log.Debug("Connected.");
								var foo = ac.GetState();
								count = foo[0];
								bri = foo[0];
								ac.Disconnect();
								ac.Dispose();
							} else {
								Log.Debug("Not connected...");
							}
						} catch (Exception e) {
							Log.Debug("Discovery exception: " + e.Message + " at " + e.StackTrace);
						}


						var data = new AdalightData(dev.Key, count);
						if (bri != 0) {
							data.Brightness = bri;
						}

						ControlService.AddDevice(data).ConfigureAwait(false);
					}
				} catch (Exception e) {
					Log.Debug("Exception: " + e.Message);
				}
			}, ct);
			await discoTask;
			Log.Debug("Adalight: Discovery complete.");
		}
		private static Dictionary<int, KeyValuePair<int, int>> FindDevices()
		{
			Dictionary<int, KeyValuePair<int, int>> dictionary = new();
			foreach (string portName in SerialPort.GetPortNames()){
				Log.Debug("Testing port: " + portName);
				try
				{
					int key = int.Parse(portName.Replace("COM", ""));
					SerialPort serialPort = new()
					{
						PortName = portName,
						BaudRate = 115200,
						Parity = Parity.None,
						DataBits = 8,
						StopBits = StopBits.One,
						ReadTimeout = 1500
					};
					serialPort.Open();
					if (serialPort.IsOpen) {
						var line = serialPort.ReadLine();
						Log.Debug("Response line: " + line);
						if (line.Substring(0, 3) == "Ada")
							dictionary[key] = new KeyValuePair<int, int>(0, 0);
						serialPort.Close();
					} else {
						Log.Debug($"Unable to open port {portName}.");
					}
				}
				catch (Exception ex)
				{
					Log.Warning("Exception: " + ex.Message);
				}
			}
			return dictionary;
		}
	}
}