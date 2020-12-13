#region

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using Q42.HueApi;
using Q42.HueApi.Models.Bridge;
using Q42.HueApi.Models.Groups;

#endregion

namespace Glimmr.Models.StreamingDevice.Hue {
	[Serializable]
	public class HueData : StreamingData {

		[JsonProperty] public int GroupNumber { get; set; }
		[JsonProperty] public string User { get; set; } = "";
		[JsonProperty] public string Key { get; set; } = "";
		[JsonProperty] public string GroupName { get; set; } = "";
		[JsonProperty] public string SelectedGroup { get; set; } = "";
		
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)] 
		public List<LightMap> MappedLights { get; set; } = new List<LightMap>();

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public List<Group> Groups { get; set; } = new List<Group>();

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public List<LightData> Lights { get; set; } = new List<LightData>();
		
		public HueData() {
			Tag = "HueBridge";
		}

		public HueData(string ip, string id) {
			IpAddress = ip;
			Id = id;
			Brightness = 100;
			Tag = "HueBridge";
			Name = "HueBridge - " + id.Substring(0, 4);
		}

		public HueData(LocatedBridge b) {
			if (b == null) throw new ArgumentException("Invalid located bridge.");
			IpAddress = b.IpAddress;
			Id = b.BridgeId;
			Brightness = 100;
			Name = "Hue Bridge - " + Id.Substring(0, 4);
			User = "";
			Key = "";
			SelectedGroup = "-1";
			Groups = new List<Group>();
			Lights = new List<LightData>();
			GroupName = "";
			GroupNumber = -1;
			Tag = "HueBridge";
			MappedLights ??= new List<LightMap>();
		}


		public void CopyBridgeData(HueData existing) {
			if (existing == null) throw new ArgumentException("Invalid bridge data.");
			Key = existing.Key;
			User = existing.User;
			Enable = existing.Enable;
			if (Id == null && !string.IsNullOrEmpty(IpAddress)) Id = IpAddress;
			MappedLights ??= new List<LightMap>();
			MappedLights = existing.MappedLights;
			Lights = existing.Lights;
			Groups = existing.Groups;
			Name = "Hue Bridge - " + existing.Id.Substring(0, 4);
			SelectedGroup = existing.SelectedGroup;
			Brightness = existing.Brightness;
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

		[JsonProperty] public string Name { get; set; }
		[JsonProperty] public string Id { get; set; }
		[JsonProperty] public string Type { get; set; }
		[JsonProperty] public int Brightness { get; set; }
		[JsonProperty] public State LastState { get; set; }
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public string ModelId { get; set; }
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public int Presence { get; set; }
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public int LightLevel { get; set; }
	}
	
	[Serializable]
	public class LightMap {
		[JsonProperty]
		public int Id { get; set; }
		
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