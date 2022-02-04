#region

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

#endregion

namespace GlimmrControl.Core {
	internal class DeviceHttpConnection {
		private static DeviceHttpConnection _instance;

		private readonly HttpClient client;

		private DeviceHttpConnection() {
			client = new HttpClient();
			client.Timeout = TimeSpan.FromSeconds(5);
		}

		public static DeviceHttpConnection GetInstance() {
			return _instance ?? (_instance = new DeviceHttpConnection());
		}

		public async Task<string> Send_Glimmr_API_Call(string deviceUri, string apiCall) {
			try {
				var apiCommand = "/api/glimmr"; //Glimmr http API URI
				if (!string.IsNullOrEmpty(apiCall)) {
					apiCommand += apiCall;
				}

				Debug.WriteLine("API Command: " + deviceUri + apiCommand);
				var result = await client.GetAsync(deviceUri + apiCommand);
				if (result.IsSuccessStatusCode) {
					return await result.Content.ReadAsStringAsync();
				}

				return "err";
			} catch (Exception e) {
				Debug.WriteLine("Exception: " + e.Message);
				return null; //time-out or other connection error
			}
		}
	}
}