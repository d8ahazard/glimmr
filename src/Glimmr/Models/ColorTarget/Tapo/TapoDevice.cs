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

namespace Glimmr.Models.ColorTarget.Tapo;

public class TapoDevice : ColorTarget, IColorTarget, IDisposable {
	private static readonly HttpClient HttpClient = new() {
		Timeout = TimeSpan.FromSeconds(5)
	};

	private TapoData _data;
	private bool _disposed;
	private int _brightness;
	private int _targetSector;

	public TapoDevice(TapoData td, ColorService cs) : base(cs) {
		cs.ControlService.RefreshSystemEvent += RefreshSystem;
		_data = td ?? throw new ArgumentException("Invalid Tapo data.");
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
		set => _data = (TapoData)value;
	}

	public async Task StartStream(CancellationToken ct) {
		if (Streaming || !Enable) return;

		Log.Debug("{Tag}::Starting stream: {Id}...", _data.Tag, _data.Id);
		_targetSector = _data.TargetSector;
		await SendCommand(true, null);
		await FlashColor(Color.Black);
		Streaming = true;
		Log.Debug("{Tag}::Stream started: {Id}.", _data.Tag, _data.Id);
	}

	public async Task StopStream() {
		if (!Streaming) return;

		Log.Debug("{Tag}::Stopping stream: {Id}...", _data.Tag, _data.Id);
		Streaming = false;
		await FlashColor(Color.Black);
		await SendCommand(false, null);
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
		var dev = DataUtil.GetDevice<TapoData>(Id);
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
		var hsv = RgbToHsv(color);
		var scaledBri = (int)(hsv.v / 100.0 * (_brightness / 100.0) * 100);
		if (scaledBri < 1) scaledBri = 1;

		var deviceParams = new {
			hue = hsv.h,
			saturation = hsv.s,
			brightness = scaledBri,
			color_temp = 0
		};

		await SendCommand(true, deviceParams);
	}

	private async Task SendCommand(bool on, object? colorParams) {
		if (string.IsNullOrEmpty(_data.IpAddress)) return;

		try {
			var paramsObj = colorParams != null
				? new Dictionary<string, object> {
					["device_on"] = on,
					["hue"] = ((dynamic)colorParams).hue,
					["saturation"] = ((dynamic)colorParams).saturation,
					["brightness"] = ((dynamic)colorParams).brightness,
					["color_temp"] = ((dynamic)colorParams).color_temp
				}
				: new Dictionary<string, object> {
					["device_on"] = on
				};

			var payload = new {
				method = "set_device_info",
				@params = paramsObj
			};

			var json = JsonConvert.SerializeObject(payload);
			using var content = new StringContent(json, Encoding.UTF8, "application/json");
			await HttpClient.PostAsync($"http://{_data.IpAddress}/app", content)
				.ConfigureAwait(false);
		} catch (Exception ex) {
			Log.Debug("Tapo: Error sending command: " + ex.Message);
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

		var s = max > 0 ? delta / max * 100 : 0;
		var v = max * 100;

		return ((int)h, (int)s, (int)v);
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
