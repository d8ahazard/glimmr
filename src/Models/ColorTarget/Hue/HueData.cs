#region

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using Q42.HueApi;
using Q42.HueApi.Models.Bridge;
using Q42.HueApi.Models.Groups;

#endregion

namespace Glimmr.Models.ColorTarget.Hue {
	[Serializable]
	public class HueData : IColorTargetData {

		[JsonProperty] public int GroupNumber { get; set; }
		[JsonProperty] public string User { get; set; } = "";
		[JsonProperty] public string Token { get; set; } = "";
		[JsonProperty] public string GroupName { get; set; } = "";
		[JsonProperty] public string SelectedGroup { get; set; } = "";
		
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)] 
		public List<LightMap> MappedLights { get; set; } = new List<LightMap>();

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public List<HueGroup> Groups { get; set; } = new List<HueGroup>();

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public List<LightData> Lights { get; set; } = new List<LightData>();
		
		public HueData() {
		}

		public HueData(string ip, string id) {
			IpAddress = ip;
			Id = id;
			Brightness = 100;
		}

		public HueData(LocatedBridge b) {
			if (b == null) throw new ArgumentException("Invalid located bridge.");
			IpAddress = b.IpAddress;
			Id = IpAddress;
			Brightness = 100;
			User = "";
			Token = "";
			SelectedGroup = "-1";
			Groups = new List<HueGroup>();
			Lights = new List<LightData>();
			GroupName = "";
			GroupNumber = -1;
			MappedLights ??= new List<LightMap>();
		}


		public string LastSeen { get; set; }

		public void UpdateFromDiscovered(IColorTargetData data) {
			var input = (HueData) data;
			if (input == null) throw new ArgumentException("Invalid bridge data.");
			Lights = input.Lights;
			Groups = input.Groups;
			IpAddress = input.IpAddress;
			
		}

		public SettingsProperty[] KeyProperties { get; set; } = {
			new("custom", "hue", ""),
			new("FrameDelay", "text", "Frame Delay")
		};


		public string Name { get; set; } = "Hue Bridge";
		public string Id { get; set; }
		public string Tag { get; set; } = "Hue";
		public string IpAddress { get; set; }
		public int Brightness { get; set; }
		
		public int FrameDelay { get; set; }
		public bool Enable { get; set; }

		public void AddGroups(IEnumerable<Group> groups) {
			foreach (var group in groups) {
				Groups.Add(new HueGroup(group));
			}
		}

		public void AddLights(IEnumerable<Light> lights) {
			foreach (var light in lights) {
				Lights.Add(new LightData(light));
			}
		}
	}
	
	[Serializable]
	public class LightData {
		public LightData() {
			Name = string.Empty;
			Type = string.Empty;
			Id = string.Empty;
			ModelId = string.Empty;
		}

		public LightData(Light l) {
			if (l == null) return;
			Name = l.Name;
			Id = l.Id;
			Type = l.Type;
			ModelId = l.ModelId;
		}

		[JsonProperty] 
		public string Name { get; set; }
		
		[JsonProperty] 
		private string _id;
		
		[JsonProperty]
		public string Id {
			get => _id;
			set => _id = value;
		}

		[JsonProperty] 
		public string Type { get; set; }
		[JsonProperty] 
		public int Brightness { get; set; }
		[JsonProperty] 
		public State LastState { get; set; }
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)] 
		public string ModelId { get; set; }
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)] 
		public int Presence { get; set; }
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)] 
		public int LightLevel { get; set; }
	}

	[Serializable]
	public class HueGroup : Group {
		/// <inheritdoc />
		public HueGroup() {
			
		}
		public HueGroup(Group g) {
			Id = g.Id;
			Name = g.Name;
			Lights = g.Lights;
			Type = g.Type;
		}
		
		[JsonProperty] 
		private string _id;
		
		[JsonProperty]
		public new string Id {
			get => _id;
			set => _id = value;
		}
	}
	
	[Serializable]
	public class LightMap {
		[JsonProperty] 
		private string _id;
		
		[JsonProperty]
		public string Id {
			get => _id;
			set => _id = value;
		}
		
		[DefaultValue(-1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int TargetSector { get; set; }
		
		[DefaultValue(255)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int Brightness { get; set; }
		
		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool Override { get; set; }
		
	}
}