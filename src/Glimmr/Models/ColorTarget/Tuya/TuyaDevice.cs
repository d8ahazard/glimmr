#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Tuya;

public class TuyaDevice : ColorTarget, IColorTarget, IDisposable {
	private static readonly HttpClient HttpClient = new();

	private TuyaData _data;
	private bool _disposed;
	private int _brightness;
	private int _targetSector;

	public TuyaDevice(TuyaData td, ColorService cs) : base(cs) {
		cs.ControlService.RefreshSystemEvent += RefreshSystem;
		_data = td ?? throw new ArgumentException("Invalid Tuya data.");
		Id = _data.Id;
		_brightness = _data.Brightness;
		ReloadData();
		ColorService.ColorSendEventAsync += SetColors;
	}

	public bool Enable { get; set; }
	public bool Streaming { get; set; }
	public string Id { get; }

	IColorTargetData IColorTarget.Data {
		get => _data;
		set => _data = (TuyaData)value;
	}

	public async Task StartStream(CancellationToken ct) {
		if (Streaming || !Enable) return;

		Log.Debug("{Tag}::Starting stream: {Id}...", _data.Tag, _data.Id);
		_targetSector = _data.TargetSector;
		await FlashColor(Color.Black);
		Streaming = true;
		Log.Debug("{Tag}::Stream started: {Id}.", _data.Tag, _data.Id);
	}

	public async Task StopStream() {
		if (!Streaming) return;

		Log.Debug("{Tag}::Stopping stream: {Id}...", _data.Tag, _data.Id);
		Streaming = false;
		await FlashColor(Color.Black);
		Log.Debug("{Tag}::Stream stopped: {Id}.", _data.Tag, _data.Id);
	}

	public async Task FlashColor(Color color) {
		await SendColor(color);
	}

	public async Task SetColors(IReadOnlyList<Color> ledColors, IReadOnlyList<Color> sectorColors) {
		if (!Streaming || !Enable) return;

		if (_targetSector < 1 || _targetSector > sectorColors.Count) return;

		var color = sectorColors[_targetSector - 1];
		await SendColor(color);
	}

	public Task ReloadData() {
		var dev = DataUtil.GetDevice<TuyaData>(Id);
		if (dev != null) {
			_data = dev;
		}

		_brightness = _data.Brightness;
		_targetSector = _data.TargetSector;
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

	private async Task SendColor(Color color) {
		if (string.IsNullOrEmpty(_data.IpAddress) || string.IsNullOrEmpty(_data.DeviceId)) return;

		try {
			var hsv = RgbToHsv(color);
			var scaledV = (int)(hsv.v * (_brightness / 100.0));
			var colourData = $"{hsv.h:X4}{hsv.s:X4}{scaledV:X4}";

			var payload = new {
				devId = _data.DeviceId,
				dps = new Dictionary<string, object> {
					["colour_data_v2"] = colourData
				}
			};

			var json = JsonConvert.SerializeObject(payload);
			using var content = new StringContent(json, Encoding.UTF8, "application/json");
			await HttpClient.PostAsync($"http://{_data.IpAddress}/v1.0/devices/{_data.DeviceId}/commands", content)
				.ConfigureAwait(false);
		} catch (Exception ex) {
			Log.Debug("Tuya: Error sending color: " + ex.Message);
		}
	}

	private static (int h, int s, int v) RgbToHsv(Color color) {
		var r = color.R / 255.0;
		var g = color.G / 255.0;
		var b = color.B / 255.0;

		var max = Math.Max(r, Math.Max(g, b));
		var min = Math.Min(r, Math.Min(g, b));
		var delta = max - min;

		double h = 0;
		if (delta > 0) {
			if (max == r) h = 60.0 * ((g - b) / delta % 6);
			else if (max == g) h = 60.0 * ((b - r) / delta + 2);
			else h = 60.0 * ((r - g) / delta + 4);
		}

		if (h < 0) h += 360;

		var s = max > 0 ? delta / max : 0;
		var v = max;

		return ((int)h, (int)(s * 1000), (int)(v * 1000));
	}

	private async Task Dispose(bool disposing) {
		if (_disposed) return;

		if (disposing) {
			if (Streaming) {
				await StopStream();
			}
		}

		_disposed = true;
	}
}
