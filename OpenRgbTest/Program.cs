#region

using System;
using System.Linq;
using Newtonsoft.Json;
using OpenRGB.NET;
using OpenRGB.NET.Models;

#endregion

namespace OpenRgbTest {
	internal class Program {
		private static void Main(string[] args) {
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
						orClient.UpdateLeds(i, dev.Leds.Select(led => new Color(255, 255)).ToArray());
						i++;
					}
				}
			}
		}
	}
}