#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Hyperion;

public class HyperionDevice : ColorTarget, IColorTarget, IDisposable {
	private HyperionData _data;
	private TcpClient? _tcp;
	private NetworkStream? _stream;
	private int _priority;
	private int _ledCount;
	private bool _disposed;

	public HyperionDevice(HyperionData data, ColorService cs) : base(cs) {
		_data = data;
		Id = _data.Id;
		Enable = _data.Enable;
		_priority = _data.Priority;
		_ledCount = _data.LedCount;
		cs.ColorSendEventAsync += SetColors;
	}

	public bool Enable { get; set; }
	public bool Streaming { get; set; }
	public string Id { get; private set; }

	IColorTargetData IColorTarget.Data {
		get => _data;
		set => _data = (HyperionData)value;
	}

	public async Task StartStream(CancellationToken ct) {
		if (Streaming || !Enable) return;

		Log.Debug($"{_data.Tag}::Starting stream: {_data.Id}...");
		_priority = _data.Priority;
		_ledCount = _data.LedCount;

		try {
			_tcp = new TcpClient();
			await _tcp.ConnectAsync(_data.IpAddress, _data.Port, ct);
			_stream = _tcp.GetStream();

			var initColor = new int[_ledCount * 3];
			var initCmd = JsonConvert.SerializeObject(new {
				command = "color",
				color = initColor,
				priority = _priority,
				duration = 0
			});
			await SendCommand(initCmd);

			Streaming = true;
			Log.Debug($"{_data.Tag}::Stream started: {_data.Id}.");
		} catch (Exception e) {
			Log.Debug($"Hyperion: Failed to start stream for {_data.Id}: {e.Message}");
		}
	}

	public async Task StopStream() {
		if (!Streaming) return;

		Log.Debug($"{_data.Tag}::Stopping stream...{_data.Id}.");
		Streaming = false;

		try {
			var clearCmd = JsonConvert.SerializeObject(new {
				command = "clear",
				priority = _priority
			});
			await SendCommand(clearCmd);
		} catch (Exception e) {
			Log.Debug("Hyperion: Error sending clear: " + e.Message);
		}

		CloseConnection();
		Log.Debug($"{_data.Tag}::Stream stopped: {_data.Id}.");
	}

	public async Task FlashColor(Color color) {
		var colorArray = new int[_ledCount * 3];
		for (var i = 0; i < _ledCount; i++) {
			colorArray[i * 3] = color.R;
			colorArray[i * 3 + 1] = color.G;
			colorArray[i * 3 + 2] = color.B;
		}

		var cmd = JsonConvert.SerializeObject(new {
			command = "color",
			color = colorArray,
			priority = _priority
		});
		await SendCommand(cmd);
	}

	public async Task SetColors(IReadOnlyList<Color> ledColors, IReadOnlyList<Color> sectorColors) {
		if (!Streaming || !Enable) return;

		var colorArray = new int[_ledCount * 3];
		var sourceCount = Math.Min(ledColors.Count, _ledCount);

		for (var i = 0; i < _ledCount; i++) {
			var col = i < sourceCount ? ledColors[i] : Color.Black;
			colorArray[i * 3] = col.R;
			colorArray[i * 3 + 1] = col.G;
			colorArray[i * 3 + 2] = col.B;
		}

		var cmd = JsonConvert.SerializeObject(new {
			command = "color",
			color = colorArray,
			priority = _priority
		});
		await SendCommand(cmd);
	}

	public Task ReloadData() {
		var dev = DataUtil.GetDevice<HyperionData>(Id);
		if (dev != null) _data = dev;
		Enable = _data.Enable;
		_priority = _data.Priority;
		_ledCount = _data.LedCount;
		return Task.CompletedTask;
	}

	public void Dispose() {
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	private Task SetColors(object sender, ColorSendEventArgs args) {
		return SetColors(args.LedColors, args.SectorColors);
	}

	private async Task SendCommand(string json) {
		if (_stream == null || _tcp is not { Connected: true }) return;

		try {
			var bytes = Encoding.UTF8.GetBytes(json + "\n");
			await _stream.WriteAsync(bytes);
			await _stream.FlushAsync();
		} catch (Exception e) {
			Log.Debug("Hyperion: Error sending command: " + e.Message);
		}
	}

	private void CloseConnection() {
		_stream?.Dispose();
		_stream = null;
		_tcp?.Dispose();
		_tcp = null;
	}

	protected virtual void Dispose(bool disposing) {
		if (_disposed) return;
		if (disposing) {
			if (Streaming) StopStream().ConfigureAwait(false);
			CloseConnection();
		}

		_disposed = true;
	}
}
