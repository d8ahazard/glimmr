﻿#region

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
				var devs = new Dictionary<string, KeyValuePair<int,int>>();
				try {
					devs = FindDevices();
				} catch (Exception e) {
					Log.Debug("Exception: " + e.Message);
				}
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
							Log.Debug("State got, done.");
						} else {
							Log.Debug("Not connected...");
						}
					} catch (Exception e) {
						Log.Debug("Discovery exception: " + e.Message + " at " + e.StackTrace);
					}


					var data = new AdalightData(dev.Key, count);
					Log.Debug("Creating device.");
					if (bri != 0) {
						data.Brightness = bri;
					}
					
					ControlService.AddDevice(data).ConfigureAwait(false);
					Log.Debug("And added...");
				}
				
			}, ct);
			await discoTask;
			Log.Debug("Adalight: Discovery complete.");
		}
		private static Dictionary<string, KeyValuePair<int, int>> FindDevices()
		{
			Dictionary<string, KeyValuePair<int, int>> dictionary = new();
			foreach (string portName in SerialPort.GetPortNames()){
				if (!portName.Contains("COM") && !portName.Contains("ttyACM")) {
					continue;
				}
				Log.Debug("Testing port: " + portName);
				try
				{
					SerialPort serialPort = new()
					{
						PortName = portName,
						BaudRate = 115200,
						Parity = Parity.None,
						DataBits = 8,
						StopBits = StopBits.One,
						ReadTimeout = 1500
					};
					Log.Debug("Opening...");
					serialPort.Open();
					Log.Debug("Opened.");
					if (serialPort.IsOpen) {
						Log.Debug("Reading");
						var line = serialPort.ReadLine();
						Log.Debug("Response line: " + line);
						if (line.Substring(0, 3).ToLower()== "ada") {
							Log.Debug("Line match.");
							dictionary[portName] = new KeyValuePair<int, int>(0, 0);
						}
						serialPort.Close();
					} else {
						Log.Debug($"Unable to open port {portName}.");
					}
				}
				catch (Exception ex){
					Log.Warning($"Exception testing port {portName}: " + ex.Message + " at " + ex.StackTrace);
				}
			}
			return dictionary;
		}
	}
}