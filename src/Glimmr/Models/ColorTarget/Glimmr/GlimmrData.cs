#region

using System;
using System.Globalization;
using System.Net;
using Glimmr.Models.Util;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget.Glimmr {
	[Serializable]
	public class GlimmrData : IColorTargetData {
		/// <summary>
		///     Mirror light data before sending to target Glimmr.
		/// </summary>
		[JsonProperty]
		public bool MirrorHorizontal { get; set; }

		/// <summary>
		///     Number of LEDs along the bottom side of the screen.
		/// </summary>
		[JsonProperty]
		public int BottomCount { get; set; }

		/// <summary>
		///     Device brightness.
		/// </summary>
		[JsonProperty]
		public int Brightness { get; set; } = 255;

		/// <summary>
		///     Number of LEDs along the left side of the screen.
		/// </summary>
		[JsonProperty]
		public int LeftCount { get; set; }

		/// <summary>
		///     Number of LEDs along the right side of the screen.
		/// </summary>
		[JsonProperty]
		public int RightCount { get; set; }

		/// <summary>
		///     Number of LEDs along the top side of the screen.
		/// </summary>
		[JsonProperty]
		public int TopCount { get; set; }


		public GlimmrData() {
			Tag = "Glimmr";
			Name ??= Tag;
			if (Id != null) {
				Name = StringUtil.UppercaseFirst(Id);
			}

			LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
		}

		public GlimmrData(string id, IPAddress ip) {
			Id = id;
			Tag = "Glimmr";
			Name ??= Tag;
			if (Id != null) {
				Name = StringUtil.UppercaseFirst(Id);
			}

			IpAddress = ip.ToString();
			FetchData();
			LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
		}

		public GlimmrData(SystemData sd) {
			Tag = "Glimmr";
			Name ??= Tag;
			LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
			LeftCount = sd.LeftCount;
			RightCount = sd.RightCount;
			TopCount = sd.TopCount;
			BottomCount = sd.BottomCount;
			IpAddress = IpUtil.GetLocalIpAddress();
			Id = Dns.GetHostName();
		}

		/// <summary>
		///     Device tag.
		/// </summary>
		[JsonProperty]
		public string Tag { get; set; }

		/// <summary>
		///     Device name.
		/// </summary>
		[JsonProperty]
		public string Name { get; set; } = "";

		/// <summary>
		///     Unique device ID.
		/// </summary>
		[JsonProperty]
		public string Id { get; set; } = "";

		/// <summary>
		///     Device IP Address.
		/// </summary>
		[JsonProperty]
		public string IpAddress { get; set; } = "";

		/// <summary>
		///     Enable device for streaming.
		/// </summary>
		[JsonProperty]
		public bool Enable { get; set; }

		/// <summary>
		///     Last time the device was seen during discovery.
		/// </summary>
		public string LastSeen { get; set; }


		public void UpdateFromDiscovered(IColorTargetData data) {
			var input = (GlimmrData)data;
			if (input == null) {
				throw new ArgumentNullException(nameof(data));
			}

			IpAddress = data.IpAddress;
			FetchData();
			if (Id != null) {
				Name = StringUtil.UppercaseFirst(Id);
			}
		}

		/// <summary>
		///     UI Properties.
		/// </summary>
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public SettingsProperty[] KeyProperties { get; set; } = {
			new("MirrorHorizontal", "check", "Mirror LED Colors")
				{ ValueHint = "Horizontally Mirror color data, for setups 'opposite' your main setup." }
		};

		private void FetchData() {
			using var webClient = new WebClient();
			try {
				var url = "http://" + IpAddress + "/api/glimmr/glimmrData";
				var jsonData = webClient.DownloadString(url);
				var sd = JsonConvert.DeserializeObject<GlimmrData>(jsonData);
				if (sd == null) {
					return;
				}

				LeftCount = sd.LeftCount;
				RightCount = sd.RightCount;
				TopCount = sd.TopCount;
				BottomCount = sd.BottomCount;
				Brightness = sd.Brightness;
			} catch (Exception) {
				// Ignored
			}
		}
	}
}