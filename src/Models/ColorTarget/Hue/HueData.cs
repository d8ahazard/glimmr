#region

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using Q42.HueApi;
using Q42.HueApi.Models.Bridge;
using Q42.HueApi.Models.Groups;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Hue {
	[Serializable]
	public class HueData : IColorTargetData {
		[JsonProperty] public int GroupNumber { get; set; }

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public List<HueGroup> Groups { get; set; } = new();

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public List<LightData> Lights { get; set; } = new();

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public List<LightMap> MappedLights { get; set; } = new();

		[JsonProperty] public string GroupName { get; set; } = "";
		[JsonProperty] public string SelectedGroup { get; set; } = "";
		[JsonProperty] public string Token { get; set; } = "";
		[JsonProperty] public string User { get; set; } = "";
		
		
		public string Name { get; set; }
		public string Id { get; set; }
		public string Tag { get; set; } = "Hue";
		public string IpAddress { get; set; }
		public int Brightness { get; set; }
		public bool Enable { get; set; }

		public HueData() {
		}

		public HueData(LocatedBridge b) {
			if (b == null) {
				throw new ArgumentException("Invalid located bridge.");
			}

			IpAddress = b.IpAddress;
			Id = b.BridgeId;
			if (Id.Length > 12) {
				Log.Debug("Truncating ID: " + Id);
				var left = Id.Substring(0, 6);
				var right = Id.Substring(Id.Length - 6);
				Id = left + right;
			}
			Log.Debug("Id should be " + Id);
			Brightness = 100;
			User = "";
			Token = "";
			SelectedGroup = "-1";
			Groups = new List<HueGroup>();
			Lights = new List<LightData>();
			GroupName = "";
			GroupNumber = -1;
			Name = "Hue - " + Id.Substring(Id.Length - 5, 4);
			MappedLights ??= new List<LightMap>();
		}


		public string LastSeen { get; set; }

		public void UpdateFromDiscovered(IColorTargetData data) {
			var input = (HueData) data;
			if (input == null) {
				throw new ArgumentException("Invalid bridge data.");
			}

			if (input.Token != null) {
				Token = input.Token;
				User = input.User;
			}

			Lights = input.Lights;
			Groups = input.Groups;
			IpAddress = input.IpAddress;
			Name = "Hue - " + Id.Substring(Id.Length - 5, 4);
		}

		public SettingsProperty[] KeyProperties { get; set; } = {
			new("custom", "hue", "")
		};



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
		[JsonProperty] public int Brightness { get; set; }

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public int LightLevel { get; set; }

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public int Presence { get; set; }

		[JsonProperty] public State LastState { get; set; }

		[JsonProperty]
		public string Id {
			get => _id;
			set => _id = value;
		}

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string ModelId { get; set; }

		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool HasStreaming { get; set; }

		[JsonProperty] public string Name { get; set; }

		[JsonProperty] public string Type { get; set; }

		[JsonProperty] private string _id;

		public LightData() {
			Name = string.Empty;
			Type = string.Empty;
			Id = string.Empty;
			ModelId = string.Empty;
		}

		public LightData(Light l) {
			if (l == null) {
				return;
			}

			Name = l.Name;
			Id = l.Id;
			Type = l.Type;
			ModelId = l.ModelId;
			HasStreaming = l.Capabilities.Streaming.Renderer;
		}
	}

	[Serializable]
	public class HueGroup : Group {
		[JsonProperty]
		public new string Id {
			get => _id;
			set => _id = value;
		}

		[JsonProperty] private string _id;

		/// <inheritdoc />
		public HueGroup() {
		}

		public HueGroup(Group g) {
			Id = g.Id;
			Name = g.Name;
			Lights = g.Lights;
			Type = g.Type;
		}
	}

	[Serializable]
	public class LightMap {
		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool Override { get; set; }

		[DefaultValue(255)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int Brightness { get; set; }

		[DefaultValue(-1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int TargetSector { get; set; }

		[JsonProperty]
		public string Id {
			get => _id;
			set => _id = value;
		}

		[JsonProperty] private string _id;
	}
}