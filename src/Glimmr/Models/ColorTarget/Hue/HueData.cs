#region

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Q42.HueApi;
using Q42.HueApi.Models.Bridge;
using Q42.HueApi.Models.Groups;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Hue {
	[Serializable]
	public class HueData : IColorTargetData {
		/// <summary>
		/// Brightness to use for all enabled hue bulbs, unless override is specified.
		/// </summary>
		[JsonProperty] public int Brightness { get; set; } = 255;
		
		/// <summary>
		/// List of available entertainment groups.
		/// </summary>
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public List<HueGroup> Groups { get; set; } = new();
		
		/// <summary>
		/// List of available lights.
		/// </summary>
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public List<LightData> Lights { get; set; } = new();
		
		/// <summary>
		/// List of lights and their mappings to sectors.
		/// </summary>

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public List<LightMap> MappedLights {
			get => _lights;
			set {
				var ids = new List<string>();
				_lights = new List<LightMap>();
				foreach (var light in value.Where(light => !ids.Contains(light.Id))) {
					ids.Add(light.Id);	
					_lights.Add(light);
				}
			}
		}

		private List<LightMap> _lights = new();
		
		/// <summary>
		/// Target entertainment group to use for streaming.
		/// </summary>
		[JsonProperty] public string SelectedGroup { get; set; } = "";
		
		/// <summary>
		/// Device tag
		/// </summary>
		[JsonProperty] public string Tag { get; set; } = "Hue";
		
		/// <summary>
		/// Token for entertainment streaming assigned after device is linked.
		/// </summary>
		[JsonProperty] public string Token { get; set; } = "";
		
		/// <summary>
		/// Hue user ID assigned after device is linked.
		/// </summary>
		[JsonProperty] public string User { get; set; } = "";

		public HueData() {
			LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
		}

		public HueData(LocatedBridge b) {
			LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
			if (b == null) {
				throw new ArgumentException("Invalid located bridge.");
			}

			IpAddress = b.IpAddress;
			Id = b.BridgeId;
			if (Id.Length > 12) {
				var left = Id[..6];
				var right = Id[^6..];
				Id = left + right;
			}

			Brightness = 100;
			User = "";
			Token = "";
			SelectedGroup = "-1";
			Groups = new List<HueGroup>();
			Lights = new List<LightData>();
			Name = "Hue - " + Id.Substring(Id.Length - 5, 4);
			MappedLights ??= new List<LightMap>();
		}

		/// <summary>
		/// Device name.
		/// </summary>
		[JsonProperty] public string Name { get; set; } = "";
		
		/// <summary>
		/// Unique device ID.
		/// </summary>
		[JsonProperty] public string Id { get; set; } = "";
		
		/// <summary>
		/// Device IP Address.
		/// </summary>
		[JsonProperty] public string IpAddress { get; set; } = "";
		
		/// <summary>
		/// Enable device for streaming.
		/// </summary>
		[JsonProperty] public bool Enable { get; set; }

		/// <summary>
		/// Last time the device was seen during discovery.
		/// </summary>
		public string LastSeen { get; set; }


		public void UpdateFromDiscovered(IColorTargetData data) {
			var input = (HueData) data;
			if (input == null) {
				throw new ArgumentException("Invalid bridge data.");
			}

			if (!string.IsNullOrEmpty(input.Token)) {
				if (Token != input.Token || User != input.User) {
					Log.Debug("Updating token and user to " + Token + " and " + User);
					Token = input.Token;
					User = input.User;
				}
			}

			Lights = input.Lights;
			Groups = input.Groups;
			IpAddress = input.IpAddress;
			Name = "Hue - " + Id.Substring(Id.Length - 5, 4);
		}

		/// <summary>
		/// UI Properties.
		/// </summary>
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
		/// <summary>
		/// If Light is capable of streaming.
		/// </summary>
		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool HasStreaming { get; set; }
		/// <summary>
		/// Current light brightness.
		/// </summary>

		[JsonProperty] public int Brightness { get; set; }
		
		/// <summary>
		/// Light elevation.
		/// </summary>

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public int LightLevel { get; set; }
		
		/// <summary>
		/// ?
		/// </summary>

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public int Presence { get; set; }

		/// <summary>
		/// Previous light state.
		/// </summary>
		[JsonProperty] public State? LastState { get; set; }

		/// <summary>
		/// Bulb ID.
		/// </summary>
		[JsonProperty]
		public string Id {
			get => _id;
			set => _id = value;
		}

		/// <summary>
		/// Bulb model ID.
		/// </summary>
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string ModelId { get; set; } = "";

		/// <summary>
		/// Bulb name.
		/// </summary>
		[JsonProperty] public string Name { get; set; } = "";
		
		/// <summary>
		/// Bulb type.
		/// </summary>
		[JsonProperty] public string Type { get; set; } = "";

		[JsonProperty] private string _id = "";

		public LightData() {
			Name = string.Empty;
			Type = string.Empty;
			Id = string.Empty;
			ModelId = string.Empty;
			LastState = new State();
			_id = string.Empty;
		}

		public LightData(Light l) {
			if (l == null) {
				return;
			}

			LastState = new State();
			Name = l.Name;
			Id = l.Id;
			Type = l.Type;
			ModelId = l.ModelId;
			HasStreaming = l.Capabilities.Streaming.Renderer;
		}
	}

	/// <summary>
	/// Hue Entertainment Group.
	/// </summary>
	[Serializable]
	public class HueGroup : Group {
		/// <summary>
		/// Entertainment group ID.
		/// </summary>
		[JsonProperty]
		public new string Id { get; set; } = "";

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

	/// <summary>
	/// Used to associate light data with mapping/brightness info.
	/// </summary>
	[Serializable]
	public class LightMap {
		/// <summary>
		/// Override hue brightness and use light-specific value.
		/// </summary>
		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool Override { get; set; }
		
		/// <summary>
		/// Light-specific brightness - needs override enable to be used.
		/// </summary>

		[DefaultValue(255)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int Brightness { get; set; }

		/// <summary>
		/// Target sector used for streaming.
		/// </summary>
		[DefaultValue(-1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int TargetSector { get; set; }

		[JsonProperty]
		public string Id {
			get => _id;
			set => _id = value;
		}

		[JsonProperty] private string _id = "";
	}
}