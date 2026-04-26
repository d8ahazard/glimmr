#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.HomeAssistant;

public class HomeAssistantDevice : ColorTarget, IColorTarget, IDisposable {
	private HomeAssistantData _data;
	private ClientWebSocket? _ws;
	private int _msgId = 1;
	private int _brightness;
	private int _targetSector;
	private bool _disposed;
	private CancellationTokenSource? _cts;

	public HomeAssistantDevice(HomeAssistantData data, ColorService cs) : base(cs) {
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
		set => _data = (HomeAssistantData)value;
	}

	public async Task StartStream(CancellationToken ct) {
		if (Streaming || !Enable) return;

		Log.Debug($"{_data.Tag}::Starting stream: {_data.Id}...");
		_targetSector = _data.TargetSector;
		_brightness = _data.Brightness;

		try {
			_cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			_ws = new ClientWebSocket();
			var uri = new Uri($"ws://{_data.HaUrl}/api/websocket");
			await _ws.ConnectAsync(uri, _cts.Token);

			await ReceiveMessage(_cts.Token);

			var authMsg = JsonConvert.SerializeObject(new { type = "auth", access_token = _data.AccessToken });
			await SendMessage(authMsg, _cts.Token);

			var authResult = await ReceiveMessage(_cts.Token);
			if (authResult != null) {
				var authObj = JObject.Parse(authResult);
				if (authObj["type"]?.ToString() != "auth_ok") {
					Log.Warning("HomeAssistant: Auth failed for " + _data.Id);
					return;
				}
			}

			Streaming = true;
			Log.Debug($"{_data.Tag}::Stream started: {_data.Id}.");
		} catch (Exception e) {
			Log.Debug($"HomeAssistant: Failed to start stream for {_data.Id}: {e.Message}");
		}
	}

	public async Task StopStream() {
		if (!Streaming) return;

		Log.Debug($"{_data.Tag}::Stopping stream...{_data.Id}.");
		Streaming = false;

		try {
			await SendColorCommand(0, 0, 0, 0);
			if (_ws is { State: WebSocketState.Open }) {
				await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Stop", CancellationToken.None);
			}
		} catch (Exception e) {
			Log.Debug("HomeAssistant: Error stopping stream: " + e.Message);
		}

		_ws?.Dispose();
		_ws = null;
		_cts?.Dispose();
		_cts = null;
		Log.Debug($"{_data.Tag}::Stream stopped: {_data.Id}.");
	}

	public async Task FlashColor(Color color) {
		await SendColorCommand(color.R, color.G, color.B, _brightness);
	}

	public async Task SetColors(IReadOnlyList<Color> ledColors, IReadOnlyList<Color> sectorColors) {
		if (!Streaming || !Enable) return;
		if (_targetSector < 1 || _targetSector > sectorColors.Count) return;

		var col = sectorColors[_targetSector - 1];
		if (_brightness < 100) {
			col = ColorUtil.ClampBrightness(col, (int)(_brightness * 2.55f));
		}

		await SendColorCommand(col.R, col.G, col.B, _brightness);
	}

	public Task ReloadData() {
		var dev = DataUtil.GetDevice<HomeAssistantData>(Id);
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

	private async Task SendColorCommand(int r, int g, int b, int brightnessPct) {
		if (_ws is not { State: WebSocketState.Open }) return;

		try {
			var id = Interlocked.Increment(ref _msgId);
			var payload = new {
				id,
				type = "call_service",
				domain = "light",
				service = "turn_on",
				service_data = new {
					entity_id = _data.EntityId,
					rgb_color = new[] { r, g, b },
					brightness_pct = Math.Clamp(brightnessPct, 0, 100)
				}
			};

			var msg = JsonConvert.SerializeObject(payload);
			await SendMessage(msg, _cts?.Token ?? CancellationToken.None);
		} catch (Exception e) {
			Log.Debug("HomeAssistant: Error sending color: " + e.Message);
		}
	}

	private async Task SendMessage(string message, CancellationToken ct) {
		if (_ws == null) return;
		var bytes = Encoding.UTF8.GetBytes(message);
		await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
	}

	private async Task<string?> ReceiveMessage(CancellationToken ct) {
		if (_ws == null) return null;
		var buffer = new byte[4096];
		var sb = new StringBuilder();
		WebSocketReceiveResult result;
		do {
			result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
			sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
		} while (!result.EndOfMessage);

		return sb.Length > 0 ? sb.ToString() : null;
	}

	protected virtual void Dispose(bool disposing) {
		if (_disposed) return;
		if (disposing) {
			if (Streaming) StopStream().ConfigureAwait(false);
			_ws?.Dispose();
			_cts?.Dispose();
		}

		_disposed = true;
	}
}
