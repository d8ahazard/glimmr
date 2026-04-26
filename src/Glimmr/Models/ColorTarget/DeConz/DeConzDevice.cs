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

namespace Glimmr.Models.ColorTarget.DeConz;

public class DeConzDevice : ColorTarget, IColorTarget, IDisposable {
	private static readonly HttpClient HttpClient = new();

	private DeConzData _data;
	private int _brightness;
	private int _targetSector;
	private string _stateUrl;
	private bool _disposed;

	public DeConzDevice(DeConzData data, ColorService cs) : base(cs) {
		_data = data ?? throw new ArgumentException("Invalid DeConz data.");
		Id = _data.Id;
		_brightness = _data.Brightness;
		_targetSector = _data.TargetSector;
		_stateUrl = BuildStateUrl();
		cs.ControlService.RefreshSystemEvent += RefreshSystem;
		ReloadData();
		cs.ColorSendEventAsync += SetColors;
	}

	public bool Enable { get; set; }
	public bool Streaming { get; set; }
	public string Id { get; }

	IColorTargetData IColorTarget.Data {
		get => _data;
		set => _data = (DeConzData)value;
	}

	public async Task StartStream(CancellationToken ct) {
		if (Streaming || !Enable) {
			return;
		}

		Log.Debug($"{_data.Tag}::Starting stream: {_data.Id}...");
		await SendState(true, _brightness, null, null);
		Streaming = true;
		Log.Debug($"{_data.Tag}::Stream started: {_data.Id}.");
	}

	public async Task StopStream() {
		if (!Streaming) {
			return;
		}

		Log.Debug($"{_data.Tag}::Stopping stream: {_data.Id}...");
		Streaming = false;
		await SendState(false, 0, null, null);
		Log.Debug($"{_data.Tag}::Stream stopped: {_data.Id}.");
	}

	public async Task FlashColor(Color color) {
		var (x, y) = RgbToCieXy(color.R, color.G, color.B);
		var bri = (int)(color.GetBrightness() * 255);
		await SendState(true, bri, x, y);
	}

	public async Task SetColors(IReadOnlyList<Color> ledColors, IReadOnlyList<Color> sectorColors) {
		if (!Streaming || !Enable) {
			return;
		}

		Color color;
		if (_targetSector > 0 && _targetSector <= sectorColors.Count) {
			color = sectorColors[_targetSector - 1];
		} else {
			int sumR = 0, sumG = 0, sumB = 0;
			foreach (var c in sectorColors) {
				sumR += c.R;
				sumG += c.G;
				sumB += c.B;
			}

			var count = sectorColors.Count > 0 ? sectorColors.Count : 1;
			color = Color.FromArgb(sumR / count, sumG / count, sumB / count);
		}

		var (x, y) = RgbToCieXy(color.R, color.G, color.B);
		await SendState(true, _brightness, x, y);
	}

	public Task ReloadData() {
		var dev = DataUtil.GetDevice<DeConzData>(Id);
		if (dev != null) {
			_data = dev;
		}

		_brightness = _data.Brightness;
		_targetSector = _data.TargetSector;
		Enable = _data.Enable;
		_stateUrl = BuildStateUrl();
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

	private string BuildStateUrl() {
		return $"http://{_data.IpAddress}:{_data.Port}/api/{_data.ApiKey}/lights/{_data.LightId}/state";
	}

	private async Task SendState(bool on, int bri, double? x, double? y) {
		if (string.IsNullOrEmpty(_stateUrl) || string.IsNullOrEmpty(_data.LightId)) {
			return;
		}

		try {
			var sb = new StringBuilder();
			sb.Append($"{{\"on\":{(on ? "true" : "false")},\"bri\":{Math.Clamp(bri, 0, 255)}");
			if (x.HasValue && y.HasValue) {
				sb.Append($",\"xy\":[{x.Value:F4},{y.Value:F4}]");
			}

			sb.Append('}');

			using var content = new StringContent(sb.ToString(), Encoding.UTF8, "application/json");
			using var request = new HttpRequestMessage(HttpMethod.Put, _stateUrl);
			request.Content = content;
			await HttpClient.SendAsync(request);
		} catch (Exception e) {
			Log.Debug("DeConz: Error sending state - " + e.Message);
		}
	}

	/// <summary>
	///     Convert RGB (0-255) to CIE 1931 xy chromaticity coordinates.
	/// </summary>
	private static (double x, double y) RgbToCieXy(int r, int g, int b) {
		var red = r / 255.0;
		var green = g / 255.0;
		var blue = b / 255.0;

		red = red > 0.04045 ? Math.Pow((red + 0.055) / 1.055, 2.4) : red / 12.92;
		green = green > 0.04045 ? Math.Pow((green + 0.055) / 1.055, 2.4) : green / 12.92;
		blue = blue > 0.04045 ? Math.Pow((blue + 0.055) / 1.055, 2.4) : blue / 12.92;

		var X = red * 0.4124564 + green * 0.3575761 + blue * 0.1804375;
		var Y = red * 0.2126729 + green * 0.7151522 + blue * 0.0721750;
		var Z = red * 0.0193339 + green * 0.1191920 + blue * 0.9503041;

		var sum = X + Y + Z;
		if (sum == 0) {
			return (0.3127, 0.3290);
		}

		return (X / sum, Y / sum);
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
