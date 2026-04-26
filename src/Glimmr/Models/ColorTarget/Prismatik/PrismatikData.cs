#region

using System;
using System.Globalization;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget.Prismatik;

[Serializable]
public class PrismatikData : IColorTargetData {
	[JsonProperty]
	public int Port { get; set; } = 3636;

	[JsonProperty]
	public int LedCount { get; set; }

	[JsonProperty]
	public string ApiKey { get; set; } = "";

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

	public PrismatikData() {
		Tag = "Prismatik";
		Name ??= Tag;
		Id = "";
		IpAddress = "";
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
	}

	public PrismatikData(string id, string ip, int port, string name, int ledCount) {
		Tag = "Prismatik";
		Id = id;
		IpAddress = ip;
		Port = port;
		Name = string.IsNullOrEmpty(name) ? "Prismatik " + ip : name;
		LedCount = ledCount;
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
	}

	public void UpdateFromDiscovered(IColorTargetData data) {
		var input = (PrismatikData)data;
		if (input == null) {
			throw new ArgumentNullException(nameof(data));
		}

		IpAddress = input.IpAddress;
		Port = input.Port;
		Name = input.Name;
		if (input.LedCount > 0) {
			LedCount = input.LedCount;
		}
	}

	public SettingsProperty[] KeyProperties {
		get => Kps();
		set { }
	}

	private SettingsProperty[] Kps() {
		return new[] {
			new SettingsProperty("LedCount", "number", "LED Count") { ValueMin = "1", ValueMax = "500" },
			new SettingsProperty("Port", "number", "API Port") {
				ValueMin = "1", ValueMax = "65535",
				ValueHint = "Default is 3636."
			},
			new SettingsProperty("ApiKey", "text", "API Key") {
				ValueHint = "Optional API key if Prismatik requires authentication."
			}
		};
	}
}
