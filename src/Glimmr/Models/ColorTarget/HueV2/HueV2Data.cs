#region

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using HueApi.BridgeLocator;
using HueApi.Models;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.HueV2;

[Serializable]
public class HueV2Data : IColorTargetData {
	/// <summary>
	///     Brightness to use for all enabled hue bulbs, unless override is specified.
	/// </summary>
	[JsonProperty]
	public int Brightness { get; set; } = 255;

	/// <summary>
	///     List of available entertainment groups.
	/// </summary>
	[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
	public List<HueGroup> Groups { get; set; } = new();

	/// <summary>
	///     Hue user ID assigned after device is linked.
	/// </summary>
	[JsonProperty]
	public string AppKey { get; set; } = "";

	/// <summary>
	///     Target entertainment group to use for streaming.
	/// </summary>
	[JsonProperty]
	public string SelectedGroup { get; set; } = "";

	/// <summary>
	///     Token for entertainment streaming assigned after device is linked.
	/// </summary>
	[JsonProperty]
	public string Token { get; set; } = "";

	public HueV2Data() {
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
	}

	public HueV2Data(LocatedBridge b) {
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

		Id += "v2";
		Brightness = 100;
		AppKey = "";
		Token = "";
		SelectedGroup = "";
		Groups = new List<HueGroup>();
	}

	/// <summary>
	///     Device tag
	/// </summary>
	[JsonProperty]
	public string Tag { get; set; } = "HueV2";

	/// <summary>
	///     Device name.
	/// </summary>
	[JsonProperty]
	public string Name {
		get =>
			Id.Length > 6
				? string.Concat("Hue ",
					Id.AsSpan(Id.Length - 5, 5).ToString().ToUpper().Replace(":", ""))
				: "Hue Bridge";
		set { }
	}

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
		var input = (HueV2Data)data;
		if (input == null) {
			throw new ArgumentException("Invalid bridge data.");
		}

		if (!string.IsNullOrEmpty(input.Token)) {
			if (Token != input.Token || AppKey != input.AppKey) {
				Log.Debug("Updating token and user to " + Token + " and " + AppKey);
				Token = input.Token;
				AppKey = input.AppKey;
			}
		}

		var ng = new List<HueGroup>();
		foreach (var group in input.Groups) {
			var services = group.Services;
			var ns = new List<LightMapV2>();
			foreach (var existingG in Groups.Where(existingG => existingG.Id == group.Id)) {
				var exServices = existingG.Services;
				foreach (var svc in services) {
					foreach (var exSvc in exServices.Where(exSvc => svc.Id == exSvc.Id)) {
						svc.TargetSector = exSvc.TargetSector;
						svc.Brightness = exSvc.Brightness;
						svc.Override = exSvc.Override;
						break;
					}

					ns.Add(svc);
				}
			}

			group.Services = ns;
			ng.Add(group);
		}

		Groups = ng;
		IpAddress = input.IpAddress;
	}

	/// <summary>
	///     UI Properties.
	/// </summary>
	public SettingsProperty[] KeyProperties { get; set; } = {
		new("custom", "hue", "")
	};

	public void ConfigureEntertainment(List<EntertainmentConfiguration> groups, List<Entertainment> devs,
		List<Light> lights, bool json = false) {
		if (groups.Count == 0) {
			Log.Debug("No groups...");
		}

		if (devs.Count == 0) {
			Log.Debug("No devices...");
		}

		if (lights.Count == 0) {
			Log.Debug("No lights...");
		}

		var ll = new List<LightMapV2>();
		var ids = new List<string>();
		foreach (var light in lights.Where(light => light.Color != null)) {
			var lMap = new LightMapV2(light, devs);
			if (!ids.Contains(lMap.Id)) {
				ll.Add(lMap);
			}

			ids.Add(lMap.Id);
		}

		foreach (var g in groups.Where(g => g.Type == "entertainment_configuration")) {
			var gMap = new HueGroup(g, ll, json);
			Groups.Add(gMap);
		}
	}
}

[Serializable]
public class HueEntertainmentConfig : EntertainmentConfiguration {
	/// <summary>
	///     Config ID
	/// </summary>
	public HueEntertainmentConfig() { }

	public HueEntertainmentConfig(EntertainmentConfiguration config) {
		Id = config.Id;
		Name = config.Name;
		Channels = config.Channels;
		Type = config.Type;
	}
}

/// <summary>
///     Used to associate light data with mapping/brightness info.
/// </summary>
[Serializable]
public class LightMapV2 {
	/// <summary>
	///     Override hue brightness and use light-specific value.
	/// </summary>
	[DefaultValue(false)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool Override { get; set; }


	/// <summary>
	///     Channel ID of light/device
	/// </summary>
	[JsonProperty]
	public Guid SvcId { get; set; }

	/// <summary>
	///     Light-specific brightness - needs override enable to be used.
	/// </summary>

	[DefaultValue(255)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int Brightness { get; set; }

	/// <summary>
	///     Device channel
	/// </summary>

	[DefaultValue(-1)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int Channel { get; set; }

	/// <summary>
	///     Target sector used for streaming.
	/// </summary>
	[DefaultValue(-1)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int TargetSector { get; set; }

	/// <summary>
	///     Light ID
	/// </summary>
	[JsonProperty]
	public string Id { get; set; }

	/// <summary>
	///     Light name
	/// </summary>
	[JsonProperty]
	public string Name { get; set; }


	/// <summary>
	///     Owner RID of the light.
	/// </summary>
	[JsonProperty]
	public string Owner { get; set; }

	/// <summary>
	///     ArchType of the device
	/// </summary>
	[JsonProperty]
	public string Type { get; set; }

	public LightMapV2() {
		Id = "";
		Name = "";
		Owner = "";
		Type = "";
		Channel = -1;
	}

	public LightMapV2(LightMapV2 l, int cId) {
		Id = l.Id;
		Name = l.Name;
		Owner = l.Owner;
		Type = l.Type;
		Channel = cId;
		SvcId = l.SvcId;
		Brightness = l.Brightness;
		Override = l.Override;
		TargetSector = l.TargetSector;
	}

	public LightMapV2(Light input, IEnumerable<Entertainment> devsData) {
		Id = input.Id.ToString();
		Owner = input.Owner.Rid.ToString();
		Channel = -1;
		foreach (var svc in devsData.Where(svc => svc.Owner != null && svc.Owner.Rid.ToString() == Owner)) {
			SvcId = svc.Id;
		}

		Brightness = 255;
		Name = string.Empty;
		Type = string.Empty;
		if (input.Metadata == null) {
			return;
		}

		Name = input.Metadata.Name;
		Type = input.Metadata.Archetype ?? "";
	}
}

[Serializable]
public class HueGroup {
	/// <summary>
	///     List of all services associated with this group.
	/// </summary>
	[JsonProperty]
	public List<LightMapV2> Services { get; set; }

	/// <summary>
	///     Group ID
	/// </summary>
	[JsonProperty]
	public string Id { get; set; } = "";

	/// <summary>
	///     Group Name
	/// </summary>
	[JsonProperty]
	public string Name { get; set; } = "";

	public HueGroup() {
		Services = new List<LightMapV2>();
	}

	public HueGroup(EntertainmentConfiguration config, List<LightMapV2> lights, bool fromJson = false) {
		Services = new List<LightMapV2>();
		Id = config.Id.ToString();
		Name = "";
		if (config.Metadata == null) {
			return;
		}

		Name = config.Metadata.Name;
		var cc = 0;
		foreach (var s in config.Channels) {
			foreach (var sm in s.Members) {
				if (sm.Service == null) {
					continue;
				}

				var cId = fromJson ? cc : s.ChannelId;
				foreach (var lMap in from l in lights where l.SvcId == sm.Service.Rid select new LightMapV2(l, cId)) {
					Services.Add(lMap);

					break;
				}
			}

			cc++;
		}
	}
}