#region

using System;
using System.Globalization;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget.SignalRgb;

[Serializable]
public class SignalRgbData : IColorTargetData {
	[JsonProperty]
	public int Brightness { get; set; } = 100;

	public SignalRgbData() {
		Tag = "SignalRgb";
		Name ??= Tag;
		Id = "";
		IpAddress = "127.0.0.1";
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
	}

	public SignalRgbData(string id, string name) {
		Id = id;
		Tag = "SignalRgb";
		Name = name;
		IpAddress = "127.0.0.1";
		Brightness = 100;
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
	}

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

	public void UpdateFromDiscovered(IColorTargetData data) {
		var input = (SignalRgbData)data;
		IpAddress = input.IpAddress;
		Name = input.Name;
		Brightness = input.Brightness;
	}

	public SettingsProperty[] KeyProperties {
		get => new[] {
			new SettingsProperty("Brightness", "number", "Brightness") {
				ValueMax = "100", ValueMin = "0", ValueStep = "1"
			}
		};
		set { }
	}
}
