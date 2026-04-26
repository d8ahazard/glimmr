#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Prismatik;

public class PrismatikDevice : ColorTarget, IColorTarget, IDisposable {
	private PrismatikData _data;
	private bool _disposed;
	private TcpClient? _tcp;
	private StreamWriter? _writer;
	private StreamReader? _reader;
	private int _ledCount;

	public PrismatikDevice(PrismatikData pd, ColorService cs) : base(cs) {
		cs.ControlService.RefreshSystemEvent += RefreshSystem;
		_data = pd ?? throw new ArgumentException("Invalid Prismatik data.");
		Id = _data.Id;
		_ledCount = _data.LedCount;
		ReloadData();
		ColorService.ColorSendEventAsync += SetColors;
	}

	public bool Enable { get; set; }
	public bool Streaming { get; set; }
	public string Id { get; }

	IColorTargetData IColorTarget.Data {
		get => _data;
		set => _data = (PrismatikData)value;
	}

	public async Task StartStream(CancellationToken ct) {
		if (Streaming || !Enable) return;

		Log.Debug("{Tag}::Starting stream: {Id}...", _data.Tag, _data.Id);

		if (!await Connect()) {
			Log.Debug("{Tag}::Failed to connect: {Id}.", _data.Tag, _data.Id);
			return;
		}

		if (!string.IsNullOrEmpty(_data.ApiKey)) {
			await SendCommand("apikey:" + _data.ApiKey);
		}

		var lockResponse = await SendCommand("lock");
		if (lockResponse != null && !lockResponse.Contains("lock:success")) {
			Log.Debug("{Tag}::Failed to acquire lock: {Response}", _data.Tag, lockResponse);
		}

		Streaming = true;
		Log.Debug("{Tag}::Stream started: {Id}.", _data.Tag, _data.Id);
	}

	public async Task StopStream() {
		if (!Streaming) return;

		Log.Debug("{Tag}::Stopping stream: {Id}...", _data.Tag, _data.Id);
		Streaming = false;

		try {
			await SendCommand("unlock");
		} catch (Exception ex) {
			Log.Debug("Prismatik: Error unlocking: " + ex.Message);
		}

		Disconnect();
		Log.Debug("{Tag}::Stream stopped: {Id}.", _data.Tag, _data.Id);
	}

	public async Task FlashColor(Color color) {
		if (!Streaming) return;

		var colors = ColorUtil.FillArray(color, _ledCount);
		await SendColors(colors);
	}

	public async Task SetColors(IReadOnlyList<Color> ledColors, IReadOnlyList<Color> sectorColors) {
		if (!Streaming || !Enable) return;

		var toSend = ledColors.Count >= _ledCount
			? ledColors.Take(_ledCount).ToArray()
			: ColorUtil.FillArray(ledColors.LastOrDefault(), _ledCount);

		await SendColors(toSend);
	}

	public Task ReloadData() {
		var dev = DataUtil.GetDevice<PrismatikData>(Id);
		if (dev != null) {
			_data = dev;
		}

		_ledCount = _data.LedCount;
		Enable = _data.Enable;
		return Task.CompletedTask;
	}

	public void Dispose() {
		Dispose(true).ConfigureAwait(true);
		GC.SuppressFinalize(this);
	}

	private Task SetColors(object sender, ColorSendEventArgs args) {
		return SetColors(args.LedColors, args.SectorColors);
	}

	private void RefreshSystem() {
		ReloadData();
	}

	private async Task<bool> Connect() {
		try {
			Disconnect();
			_tcp = new TcpClient();
			await _tcp.ConnectAsync(_data.IpAddress, _data.Port);
			var stream = _tcp.GetStream();
			_writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };
			_reader = new StreamReader(stream, Encoding.ASCII);

			var greeting = await _reader.ReadLineAsync();
			Log.Debug("Prismatik: Connected, greeting: {Greeting}", greeting);
			return true;
		} catch (Exception ex) {
			Log.Debug("Prismatik: Connection error: " + ex.Message);
			Disconnect();
			return false;
		}
	}

	private void Disconnect() {
		try {
			_writer?.Dispose();
			_reader?.Dispose();
			_tcp?.Dispose();
		} catch {
			// Ignore cleanup errors
		}

		_writer = null;
		_reader = null;
		_tcp = null;
	}

	private async Task<string?> SendCommand(string command) {
		if (_writer == null || _reader == null) return null;

		try {
			await _writer.WriteLineAsync(command);
			return await _reader.ReadLineAsync();
		} catch (Exception ex) {
			Log.Debug("Prismatik: Command error ({Command}): {Message}", command, ex.Message);
			return null;
		}
	}

	private async Task SendColors(IReadOnlyList<Color> colors) {
		if (_writer == null || _ledCount == 0) return;

		try {
			var sb = new StringBuilder("setcolor:");
			for (var i = 0; i < _ledCount && i < colors.Count; i++) {
				if (i > 0) sb.Append(';');
				var c = colors[i];
				sb.Append($"{i + 1}-{c.R},{c.G},{c.B}");
			}

			await _writer.WriteLineAsync(sb.ToString());
		} catch (Exception ex) {
			Log.Debug("Prismatik: Error sending colors: " + ex.Message);
		}
	}

	private async Task Dispose(bool disposing) {
		if (_disposed) return;

		if (disposing) {
			if (Streaming) {
				await StopStream();
			}

			Disconnect();
		}

		_disposed = true;
	}
}
