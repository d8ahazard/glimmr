#region

using System;
using System.ComponentModel;
using System.Globalization;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget.MqttLight;

[Serializable]
public class MqttLightData : IColorTargetData {
	[DefaultValue("localhost")]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public string BrokerHost { get; set; } = "localhost";

	[DefaultValue(1883)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int BrokerPort { get; set; } = 1883;

	[JsonProperty]
	public string Topic { get; set; } = "";

	[DefaultValue(100)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int Brightness { get; set; } = 100;

	[DefaultValue(-1)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int TargetSector { get; set; } = -1;

	public MqttLightData() {
		Tag = "MqttLight";
		Name ??= Tag;
		Id = "";
		IpAddress = "";
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
	}

	[DefaultValue("MqttLight")]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Tag { get; set; } = "MqttLight";

	[DefaultValue("")]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Name { get; set; } = "";

	[DefaultValue("")]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Id { get; set; } = "";

	[DefaultValue("")]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public string IpAddress { get; set; } = "";

	[JsonProperty]
	public bool Enable { get; set; }

	[JsonProperty]
	public string LastSeen { get; set; }

	public void UpdateFromDiscovered(IColorTargetData data) {
		var input = (MqttLightData)data;
		IpAddress = input.IpAddress;
		Name = input.Name;
		BrokerHost = input.BrokerHost;
		BrokerPort = input.BrokerPort;
		Topic = input.Topic;
	}

	public SettingsProperty[] KeyProperties { get; set; } = {
		new("BrokerHost", "text", "MQTT Broker Host") { ValueHint = "e.g. 192.168.1.100" },
		new("BrokerPort", "number", "MQTT Broker Port") { ValueMin = "1", ValueMax = "65535" },
		new("Topic", "text", "MQTT Set Topic") { ValueHint = "e.g. zigbee2mqtt/FRIENDLY_NAME/set" },
		new("Brightness", "number", "Brightness") { ValueMax = "100", ValueMin = "0" },
		new("TargetSector", "sectormap", "Target Sector")
	};
}
