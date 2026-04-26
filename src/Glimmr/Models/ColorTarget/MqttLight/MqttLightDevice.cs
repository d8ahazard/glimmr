#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using MQTTnet;
using MQTTnet.Client;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.MqttLight;

public class MqttLightDevice : ColorTarget, IColorTarget, IDisposable {
	private MqttLightData _data;
	private IMqttClient? _mqttClient;
	private int _brightness;
	private int _targetSector;
	private bool _disposed;

	public MqttLightDevice(MqttLightData data, ColorService cs) : base(cs) {
		_data = data;
		Id = _data.Id;
		Enable = _data.Enable;
		_brightness = _data.Brightness;
		_targetSector = _data.TargetSector;
		cs.ColorSendEventAsync += SetColors;
	}

	public bool Enable { get; set; }
	public bool Streaming { get; set; }
	public string Id { get; private set; }

	IColorTargetData IColorTarget.Data {
		get => _data;
		set => _data = (MqttLightData)value;
	}

	public async Task StartStream(CancellationToken ct) {
		if (Streaming || !Enable) return;

		Log.Debug($"{_data.Tag}::Starting stream: {_data.Id}...");
		_targetSector = _data.TargetSector;
		_brightness = _data.Brightness;

		try {
			var factory = new MqttFactory();
			_mqttClient = factory.CreateMqttClient();

			var options = new MqttClientOptionsBuilder()
				.WithTcpServer(_data.BrokerHost, _data.BrokerPort)
				.WithClientId("glimmr_" + _data.Id.Replace(":", "")[..8])
				.WithCleanSession()
				.Build();

			await _mqttClient.ConnectAsync(options, ct);
			Streaming = true;
			Log.Debug($"{_data.Tag}::Stream started: {_data.Id}.");
		} catch (Exception e) {
			Log.Debug($"MqttLight: Failed to start stream for {_data.Id}: {e.Message}");
		}
	}

	public async Task StopStream() {
		if (!Streaming) return;

		Log.Debug($"{_data.Tag}::Stopping stream...{_data.Id}.");
		Streaming = false;

		try {
			await PublishColor(0, 0, 0, 0, "OFF");
			if (_mqttClient is { IsConnected: true }) {
				await _mqttClient.DisconnectAsync();
			}
		} catch (Exception e) {
			Log.Debug("MqttLight: Error stopping stream: " + e.Message);
		}

		_mqttClient?.Dispose();
		_mqttClient = null;
		Log.Debug($"{_data.Tag}::Stream stopped: {_data.Id}.");
	}

	public async Task FlashColor(Color color) {
		await PublishColor(color.R, color.G, color.B, (int)(_brightness * 2.55f), "ON");
	}

	public async Task SetColors(IReadOnlyList<Color> ledColors, IReadOnlyList<Color> sectorColors) {
		if (!Streaming || !Enable) return;
		if (_targetSector < 1 || _targetSector > sectorColors.Count) return;

		var col = sectorColors[_targetSector - 1];
		if (_brightness < 100) {
			col = ColorUtil.ClampBrightness(col, (int)(_brightness * 2.55f));
		}

		await PublishColor(col.R, col.G, col.B, (int)(_brightness * 2.55f), "ON");
	}

	public Task ReloadData() {
		var dev = DataUtil.GetDevice<MqttLightData>(Id);
		if (dev != null) _data = dev;
		Enable = _data.Enable;
		_brightness = _data.Brightness;
		_targetSector = _data.TargetSector;
		return Task.CompletedTask;
	}

	public void Dispose() {
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	private Task SetColors(object sender, ColorSendEventArgs args) {
		return SetColors(args.LedColors, args.SectorColors);
	}

	private async Task PublishColor(int r, int g, int b, int brightness, string state) {
		if (_mqttClient is not { IsConnected: true }) return;
		if (string.IsNullOrEmpty(_data.Topic)) return;

		try {
			var payload = JsonConvert.SerializeObject(new {
				color = new { r, g, b },
				brightness = Math.Clamp(brightness, 0, 255),
				state
			});

			var message = new MqttApplicationMessageBuilder()
				.WithTopic(_data.Topic)
				.WithPayload(Encoding.UTF8.GetBytes(payload))
				.WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
				.Build();

			await _mqttClient.PublishAsync(message, CancellationToken.None);
		} catch (Exception e) {
			Log.Debug("MqttLight: Error publishing color: " + e.Message);
		}
	}

	protected virtual void Dispose(bool disposing) {
		if (_disposed) return;
		if (disposing) {
			if (Streaming) StopStream().ConfigureAwait(false);
			_mqttClient?.Dispose();
		}

		_disposed = true;
	}
}
