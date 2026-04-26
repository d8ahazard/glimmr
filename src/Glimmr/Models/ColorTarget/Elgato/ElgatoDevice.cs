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

namespace Glimmr.Models.ColorTarget.Elgato;

public class ElgatoDevice : ColorTarget, IColorTarget, IDisposable {
	private string IpAddress { get; set; }
	private const int ElgatoPort = 9123;
	private static readonly HttpClient HttpClient = new();

	private ElgatoData _data;
	private bool _disposed;
	private int _brightness;
	private int _temperature;
	private int _targetSector;

	public ElgatoDevice(ElgatoData ed, ColorService cs) : base(cs) {
		_data = ed ?? throw new ArgumentException("Invalid Elgato data.");
		Id = _data.Id;
		IpAddress = _data.IpAddress;
		_brightness = _data.Brightness;
		_temperature = _data.Temperature;
		cs.ControlService.RefreshSystemEvent += RefreshSystem;
		ReloadData();
		cs.ColorSendEventAsync += SetColors;
	}

	public bool Enable { get; set; }
	public bool Streaming { get; set; }
	public string Id { get; }

	IColorTargetData IColorTarget.Data {
		get => _data;
		set => _data = (ElgatoData)value;
	}

	public async Task StartStream(CancellationToken ct) {
		if (Streaming || !Enable) return;

		Log.Debug($"{_data.Tag}::Starting stream: {_data.Id}...");
		await SetLight(true, _brightness, _temperature);
		Streaming = true;
		Log.Debug($"{_data.Tag}::Stream started: {_data.Id}.");
	}

	public async Task StopStream() {
		if (!Streaming) return;

		Log.Debug($"{_data.Tag}::Stopping stream...{_data.Id}.");
		Streaming = false;
		await SetLight(false, 0, _temperature);
		Log.Debug($"{_data.Tag}::Stream stopped: {_data.Id}.");
	}

	public async Task FlashColor(Color color) {
		var brightness = (int)(color.GetBrightness() * 100);
		var temp = BrightnessToTemperature(color);
		await SetLight(true, brightness, temp);
	}

	public Task ReloadData() {
		var dev = DataUtil.GetDevice<ElgatoData>(Id);
		if (dev != null) _data = dev;
		_brightness = _data.Brightness;
		_temperature = _data.Temperature;
		_targetSector = _data.TargetSector;
		IpAddress = _data.IpAddress;
		Enable = _data.Enable;
		return Task.CompletedTask;
	}

	public async Task SetColors(IReadOnlyList<Color> ledColors, IReadOnlyList<Color> sectorColors) {
		if (!Streaming || !Enable) return;

		if (_targetSector <= 0 || _targetSector > sectorColors.Count) return;

		var col = sectorColors[_targetSector - 1];
		var brightness = (int)(col.GetBrightness() * _brightness);
		brightness = Math.Clamp(brightness, 0, 100);
		var temp = BrightnessToTemperature(col);
		await SetLight(true, brightness, temp);
	}

	private Task SetColors(object sender, ColorSendEventArgs args) {
		return SetColors(args.LedColors, args.SectorColors);
	}

	/// <summary>
	///     Maps a color's warmth to the Elgato mired range.
	///     Cool (blue-ish) colors map toward 143 (7000K), warm (red-ish) toward 344 (2900K).
	/// </summary>
	private static int BrightnessToTemperature(Color col) {
		var warmth = (col.R / 255f - col.B / 255f + 1f) / 2f;
		return (int)(143 + warmth * (344 - 143));
	}

	private async Task SetLight(bool on, int brightness, int temperature) {
		try {
			temperature = Math.Clamp(temperature, 143, 344);
			brightness = Math.Clamp(brightness, 0, 100);
			var body = JsonConvert.SerializeObject(new {
				numberOfLights = 1,
				lights = new[] {
					new { on = on ? 1 : 0, brightness, temperature }
				}
			});
			using var content = new StringContent(body, Encoding.UTF8, "application/json");
			await HttpClient.PutAsync($"http://{IpAddress}:{ElgatoPort}/elgato/lights", content)
				.ConfigureAwait(false);
		} catch (Exception e) {
			Log.Debug("Elgato: Exception setting light: " + e.Message);
		}
	}

	private void RefreshSystem() {
		ReloadData();
	}

	public void Dispose() {
		Dispose(true).ConfigureAwait(true);
		GC.SuppressFinalize(this);
	}

	protected virtual async Task Dispose(bool disposing) {
		if (_disposed) return;
		if (disposing && Streaming) {
			await StopStream();
		}

		_disposed = true;
	}
}
