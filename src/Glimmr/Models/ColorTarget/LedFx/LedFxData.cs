#region

using System;
using System.Globalization;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget.LedFx;

[Serializable]
public class LedFxData : IColorTargetData {
	[JsonProperty]
	public int Port { get; set; } = 8888;

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

	public LedFxData() {
		Tag = "LedFx";
		Name ??= Tag;
		Id = "";
		IpAddress = "";
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
	}

	public LedFxData(string id, string ip, int port, string name) {
		Tag = "LedFx";
		Id = id;
		IpAddress = ip;
		Port = port;
		Name = string.IsNullOrEmpty(name) ? "LedFx " + ip : name;
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
	}

	public void UpdateFromDiscovered(IColorTargetData data) {
		var input = (LedFxData)data;
		if (input == null) {
			throw new ArgumentNullException(nameof(data));
		}

		IpAddress = input.IpAddress;
		Port = input.Port;
		Name = input.Name;
	}

	public SettingsProperty[] KeyProperties {
		get => Kps();
		set { }
	}

	private SettingsProperty[] Kps() {
		return new[] {
			new SettingsProperty("Port", "number", "LedFx API Port") {
				ValueMin = "1", ValueMax = "65535",
				ValueHint = "Default is 8888."
			}
		};
	}
}
