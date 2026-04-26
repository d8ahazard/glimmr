#region

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget.Tapo;

[Serializable]
public class TapoData : IColorTargetData {
	[JsonProperty]
	public int Brightness { get; set; } = 100;

	[DefaultValue(-1)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int TargetSector { get; set; }

	[JsonProperty]
	public string Username { get; set; } = "";

	[JsonProperty]
	public string Password { get; set; } = "";

	[JsonProperty]
	public string Tag { get; set; }

	[JsonProperty]
	public string Name { get; set; }

	[JsonProperty]
	public string Id { get; set; }

	[JsonProperty]
	public string IpAddress { get; set; }

	[JsonProperty]
	public bool Enable { get; set; }

	[JsonProperty]
	public string LastSeen { get; set; }

	public TapoData() {
		Tag = "Tapo";
		Name ??= Tag;
		Id = "";
		IpAddress = "";
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
	}

	public TapoData(string id, string ip, string name) {
		Tag = "Tapo";
		Id = id;
		IpAddress = ip;
		Name = string.IsNullOrEmpty(name) ? "Tapo " + ip : name;
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
	}

	public void UpdateFromDiscovered(IColorTargetData data) {
		var input = (TapoData)data;
		if (input == null) {
			throw new ArgumentNullException(nameof(data));
		}

		IpAddress = input.IpAddress;
		Name = input.Name;
	}

	public SettingsProperty[] KeyProperties {
		get => Kps();
		set { }
	}

	private SettingsProperty[] Kps() {
		return new[] {
			new SettingsProperty("sectormap", "sectormap", ""),
			new SettingsProperty("Brightness", "number", "Brightness") { ValueMin = "1", ValueMax = "100" },
			new SettingsProperty("Username", "text", "TP-Link Account Email") {
				ValueHint = "Email used for the TP-Link/Tapo account."
			},
			new SettingsProperty("Password", "text", "TP-Link Account Password") {
				ValueHint = "Password for the TP-Link/Tapo account."
			}
		};
	}
}
