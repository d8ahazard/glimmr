using System;
using System.Collections.Generic;
using Corsair.CUE.SDK;
using Newtonsoft.Json;

namespace CorsairTest {
	class Program {
		static void Main(string[] args) {
			try {
				CUESDK.CorsairPerformProtocolHandshake();
			} catch (Exception e) {
				Console.WriteLine("Handshake exception: " + e.Message);
			}
			var devs = CUESDK.CorsairGetDeviceCount();
			Console.WriteLine($"Found {devs} devices.");
			Console.WriteLine();
			var devices = new Dictionary<CorsairDeviceType, CorsairLedPositions>();
			if (devs <= 0) {
				return;
			}

			for (var i = 0; i < devs; i++) {
				try {
					var info = CUESDK.CorsairGetDeviceInfo(i);
					Console.WriteLine("Device {i} info: " + JsonConvert.SerializeObject(info));
					var layout = CUESDK.CorsairGetLedPositionsByDeviceIndex(i);
					Console.WriteLine("Device layout: " + JsonConvert.SerializeObject(layout));
					devices[info.type] = layout;
				} catch (Exception e) {
					Console.WriteLine("Exception enumerating: " + e.Message);
				}
			}
			Console.WriteLine();
			Console.WriteLine("Final device output: " + JsonConvert.SerializeObject(devices));
		}
	}
}