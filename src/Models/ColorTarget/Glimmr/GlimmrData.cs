using System;
using System.Net;
using Glimmr.Models.Util;
using Newtonsoft.Json;

namespace Glimmr.Models.ColorTarget.Glimmr {
	public class GlimmrData : IColorTargetData {
		[JsonProperty] public int BottomCount { get; set; }

		[JsonProperty] public int BottomSectorCount { get; set; }

		[JsonProperty] public int LedCount { get; set; }
		[JsonProperty] public int LeftCount { get; set; }

		[JsonProperty] public int LeftSectorCount { get; set; }
		[JsonProperty] public int RightCount { get; set; }

		[JsonProperty] public int RightSectorCount { get; set; }
		[JsonProperty] public int TopCount { get; set; }

		[JsonProperty] public int TopSectorCount { get; set; }


		public GlimmrData() {
			Tag = "Glimmr";
			Name ??= Tag;
			if (Id != null) {
				Name = StringUtil.UppercaseFirst(Id);
			}
		}

		public GlimmrData(string id) {
			Id = id;
			Tag = "Glimmr";
			Name ??= Tag;
			if (Id != null) {
				Name = StringUtil.UppercaseFirst(Id);
			}

			IpAddress = IpUtil.GetIpFromHost(id).ToString();

			using var webClient = new WebClient();
			try {
				var url = "http://" + Id + "/json";
				var jsonData = webClient.DownloadString(url);
				var sd = JsonConvert.DeserializeObject<GlimmrData>(jsonData);
				if (sd != null) {
					LedCount = sd.LedCount;
					LeftCount = sd.LeftCount;
					RightCount = sd.RightCount;
					TopCount = sd.TopCount;
					BottomCount = sd.BottomCount;
					Brightness = sd.Brightness;
				}
			} catch (Exception) {
			}
		}

		public GlimmrData(SystemData sd) {
			LedCount = sd.LedCount;
			LeftCount = sd.LeftCount;
			RightCount = sd.RightCount;
			TopCount = sd.TopCount;
			BottomCount = sd.BottomCount;
			Brightness = sd.Brightness;
			IpAddress = IpUtil.GetLocalIpAddress();
			Id = Dns.GetHostName();
		}

		public string LastSeen { get; set; }

		public void UpdateFromDiscovered(IColorTargetData data) {
			var input = (GlimmrData) data;
			if (input == null) {
				throw new ArgumentNullException(nameof(input));
			}

			LedCount = input.LedCount;
			IpAddress = data.IpAddress;
			if (Id != null) {
				Name = StringUtil.UppercaseFirst(Id);
			}
		}

		public SettingsProperty[] KeyProperties { get; set; } = {
			new("FrameDelay", "text", "Frame Delay")
		};

		[JsonProperty] public string Name { get; set; }

		public string Id { get; set; }
		public string Tag { get; set; }
		public string IpAddress { get; set; }
		public int Brightness { get; set; }

		public int FrameDelay { get; set; }
		public bool Enable { get; set; }
	}
}