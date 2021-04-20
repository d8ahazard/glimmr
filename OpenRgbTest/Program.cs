using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using OpenRGB.NET;
using OpenRGB.NET.Models;

namespace OpenRgbTest {
	class Program {
		static void Main(string[] args) {
			Console.WriteLine("Hello World!");
			var orClient = new OpenRGBClient();
			orClient.Connect();
			if (orClient.Connected) {
				Console.WriteLine("Connected!");
				
				var devs = orClient.GetAllControllerData();
				if (devs.Length > 0) {
					Console.WriteLine("Found " + devs.Length + " devices.");
					var i = 0;
					foreach (var dev in devs) {
						Console.WriteLine("Dev: " + JsonConvert.SerializeObject(dev));
						orClient.UpdateLeds(i,dev.Leds.Select(led => new Color(255, 255, 0)).ToArray());
						i++;
					}
					
				}
				
			}
		}
	}
}