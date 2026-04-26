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
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.SignalRgb;

public class SignalRgbDevice : ColorTarget, IColorTarget, IDisposable {
	private static readonly HttpClient HttpClient = new();
	private const string BaseUrl = "http://127.0.0.1:16038/api/v1/lighting";

	private SignalRgbData _data;
	private int _brightness;
	private bool _disposed;

	public SignalRgbDevice(SignalRgbData data, ColorService cs) : base(cs) {
		_data = data ?? throw new ArgumentException("Invalid SignalRGB data.");
		Id = _data.Id;
		_brightness = _data.Brightness;
		cs.ControlService.RefreshSystemEvent += RefreshSystem;
		ReloadData();
		cs.ColorSendEventAsync += SetColors;
	}

	public bool Enable { get; set; }
	public bool Streaming { get; set; }
	public string Id { get; }

	IColorTargetData IColorTarget.Data {
		get => _data;
		set => _data = (SignalRgbData)value;
	}

	public async Task StartStream(CancellationToken ct) {
		if (Streaming || !Enable) {
			return;
		}

		Log.Debug($"{_data.Tag}::Starting stream: {_data.Id}...");
		await SetEnabled(true);
		await SetBrightness(_brightness);
		Streaming = true;
		Log.Debug($"{_data.Tag}::Stream started: {_data.Id}.");
	}

	public async Task StopStream() {
		if (!Streaming) {
			return;
		}

		Log.Debug($"{_data.Tag}::Stopping stream: {_data.Id}...");
		Streaming = false;
		await SetEnabled(false);
		Log.Debug($"{_data.Tag}::Stream stopped: {_data.Id}.");
	}

	public async Task FlashColor(Color color) {
		var bri = (int)(color.GetBrightness() * 100);
		await SetBrightness(bri);
	}

	public async Task SetColors(IReadOnlyList<Color> ledColors, IReadOnlyList<Color> sectorColors) {
		if (!Streaming || !Enable) {
			return;
		}

		int sumR = 0, sumG = 0, sumB = 0;
		foreach (var c in sectorColors) {
			sumR += c.R;
			sumG += c.G;
			sumB += c.B;
		}

		var count = sectorColors.Count > 0 ? sectorColors.Count : 1;
		var avg = Color.FromArgb(sumR / count, sumG / count, sumB / count);
		var bri = (int)(avg.GetBrightness() * 100);
		var scaled = (int)(bri / 100f * _brightness);
		await SetBrightness(scaled);
	}

	public Task ReloadData() {
		var dev = DataUtil.GetDevice<SignalRgbData>(Id);
		if (dev != null) {
			_data = dev;
		}

		_brightness = _data.Brightness;
		Enable = _data.Enable;
		return Task.CompletedTask;
	}

	public void Dispose() {
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	private Task SetColors(object sender, ColorSendEventArgs args) {
		return SetColors(args.LedColors, args.SectorColors);
	}

	private void RefreshSystem() {
		ReloadData();
	}

	private static async Task SetBrightness(int brightness) {
		try {
			var json = $"{{\"global_brightness\":{brightness}}}";
			using var content = new StringContent(json, Encoding.UTF8, "application/json");
			using var request = new HttpRequestMessage(new HttpMethod("PATCH"), BaseUrl);
			request.Content = content;
			await HttpClient.SendAsync(request);
		} catch (Exception e) {
			Log.Debug("SignalRGB: Error setting brightness - " + e.Message);
		}
	}

	private static async Task SetEnabled(bool enabled) {
		try {
			var json = $"{{\"enabled\":{(enabled ? "true" : "false")}}}";
			using var content = new StringContent(json, Encoding.UTF8, "application/json");
			using var request = new HttpRequestMessage(new HttpMethod("PATCH"), BaseUrl + "/enabled");
			request.Content = content;
			await HttpClient.SendAsync(request);
		} catch (Exception e) {
			Log.Debug("SignalRGB: Error setting enabled state - " + e.Message);
		}
	}

	protected virtual void Dispose(bool disposing) {
		if (_disposed) {
			return;
		}

		if (disposing) {
			if (Streaming) {
				StopStream().ConfigureAwait(false);
			}
		}

		_disposed = true;
	}
}
