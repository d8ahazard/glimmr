#region

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using MQTTnet;
using MQTTnet.Client;
using Newtonsoft.Json.Linq;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.MqttLight;

public class MqttLightDiscovery : ColorDiscovery, IColorDiscovery {
	public MqttLightDiscovery(ColorService cs) : base(cs) {
	}

	public async Task Discover(int timeout, CancellationToken ct) {
		Log.Debug("MqttLight: Discovery started...");

		var existingDevices = DataUtil.GetDevices<MqttLightData>("MqttLight");
		foreach (var device in existingDevices) {
			if (string.IsNullOrEmpty(device.BrokerHost)) continue;

			try {
				await DiscoverFromBroker(device.BrokerHost, device.BrokerPort, timeout, ct);
			} catch (Exception e) {
				Log.Debug("MqttLight: Discovery error: " + e.Message);
			}
		}

		Log.Debug("MqttLight: Discovery complete.");
	}

	private static async Task DiscoverFromBroker(string brokerHost, int brokerPort, int timeout,
		CancellationToken ct) {
		var factory = new MqttFactory();
		using var client = factory.CreateMqttClient();

		var options = new MqttClientOptionsBuilder()
			.WithTcpServer(brokerHost, brokerPort)
			.WithClientId("glimmr_discovery_" + Guid.NewGuid().ToString("N")[..8])
			.WithCleanSession()
			.Build();

		var tcs = new TaskCompletionSource<bool>();

		client.ApplicationMessageReceivedAsync += async e => {
			try {
				var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
				var devices = JArray.Parse(payload);

				foreach (var dev in devices) {
					var type = dev["type"]?.ToString();
					if (type != "EndDevice" && type != "Router") continue;

					var definition = dev["definition"];
					if (definition == null || definition.Type == JTokenType.Null) continue;

					var exposes = definition["exposes"] as JArray;
					if (exposes == null) continue;

					var hasColor = false;
					foreach (var expose in exposes) {
						var features = expose["features"] as JArray;
						if (features == null) continue;
						foreach (var feature in features) {
							var name = feature["name"]?.ToString();
							if (name == "color_xy" || name == "color_hs") {
								hasColor = true;
								break;
							}
						}

						if (hasColor) break;
					}

					if (!hasColor) continue;

					var friendlyName = dev["friendly_name"]?.ToString() ?? "";
					var ieeeAddr = dev["ieee_address"]?.ToString() ?? "";
					if (string.IsNullOrEmpty(friendlyName) || string.IsNullOrEmpty(ieeeAddr)) continue;

					var data = new MqttLightData {
						Id = ieeeAddr,
						Name = friendlyName,
						BrokerHost = brokerHost,
						BrokerPort = brokerPort,
						Topic = $"zigbee2mqtt/{friendlyName}/set",
						IpAddress = brokerHost
					};

					await ControlService.AddDevice(data);
					Log.Debug("MqttLight: Found device: " + friendlyName);
				}
			} catch (Exception ex) {
				Log.Debug("MqttLight: Error parsing device list: " + ex.Message);
			}

			tcs.TrySetResult(true);
		};

		await client.ConnectAsync(options, ct);
		var subscribeOptions = factory.CreateSubscribeOptionsBuilder()
			.WithTopicFilter("zigbee2mqtt/bridge/devices")
			.Build();
		await client.SubscribeAsync(subscribeOptions, ct);

		var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
		cts.CancelAfter(TimeSpan.FromSeconds(timeout));
		cts.Token.Register(() => tcs.TrySetResult(false));

		await tcs.Task;
		await client.DisconnectAsync();
	}
}
