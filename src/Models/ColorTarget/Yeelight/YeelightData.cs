using System.Collections.Generic;
using System.ComponentModel;
using Glimmr.Models.Util;
using Newtonsoft.Json;

namespace Glimmr.Models.ColorTarget.Yeelight {
	public class YeelightData : IColorTargetData {
		[DefaultValue(-1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int TargetSector { get; set; }

		public YeelightData() {
			Tag = "Yeelight";
			Name ??= Tag;
			if (Id != null) Name = StringUtil.UppercaseFirst(Id);
		}
		public YeelightData(string id) {
			Id = id;
			Tag = "Yeelight";
		}

		public string Name { get; set; }
		public string Id { get; set; }
		public string Tag { get; set; }
		public string IpAddress { get; set; }
		public int Brightness { get; set; }
		public bool Enable { get; set; }
		public string LastSeen { get; set; }

		public void CopyExisting(IColorTargetData existing) {
			var yd = (YeelightData) existing;
			Brightness = yd.Brightness;
			Enable = yd.Enable;
			TargetSector = yd.TargetSector;
			
		}

		public SettingsProperty[] KeyProperties { get; set; } = {
			new("TargetSector","sectormap", "Target Sector")
		};
	}
}