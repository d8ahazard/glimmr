#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Shelly;

public class ShellyDevice : ColorTarget, IColorTarget, IDisposable {
	private string IpAddress { get; set; }
	private static readonly HttpClient HttpClient = new();

	private ShellyData _data;
	private bool _disposed;
	private int _brightness;
	private int _targetSector;
	private bool _hasWhiteChannel;

	public ShellyDevice(ShellyData sd, ColorService cs) : base(cs) {
		_data = sd ?? throw new ArgumentException("Invalid Shelly data.");
		Id = _data.Id;
		IpAddress = _data.IpAddress;
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
		set => _data = (ShellyData)value;
	}

	public async Task StartStream(CancellationToken ct) {
		if (Streaming || !Enable) return;

		Log.Debug($"{_data.Tag}::Starting stream: {_data.Id}...");
		await SendRgbw(Color.Black, true);
		Streaming = true;
		Log.Debug($"{_data.Tag}::Stream started: {_data.Id}.");
	}

	public async Task StopStream() {
		if (!Streaming) return;

		Log.Debug($"{_data.Tag}::Stopping stream...{_data.Id}.");
		Streaming = false;
		await TurnOff();
		Log.Debug($"{_data.Tag}::Stream stopped: {_data.Id}.");
	}

	public async Task FlashColor(Color color) {
		await SendRgbw(color, true);
	}

	public Task ReloadData() {
		var dev = DataUtil.GetDevice<ShellyData>(Id);
		if (dev != null) _data = dev;
		_brightness = _data.Brightness;
		_targetSector = _data.TargetSector;
		_hasWhiteChannel = _data.HasWhiteChannel;
		IpAddress = _data.IpAddress;
		Enable = _data.Enable;
		return Task.CompletedTask;
	}

	public async Task SetColors(IReadOnlyList<Color> ledColors, IReadOnlyList<Color> sectorColors) {
		if (!Streaming || !Enable) return;

		if (_targetSector <= 0 || _targetSector > sectorColors.Count) return;

		var col = sectorColors[_targetSector - 1];
		if (_brightness < 100) {
			col = ColorUtil.ClampBrightness(col, (int)(_brightness * 2.55f));
		}

		await SendRgbw(col, true);
	}

	private Task SetColors(object sender, ColorSendEventArgs args) {
		return SetColors(args.LedColors, args.SectorColors);
	}

	private async Task SendRgbw(Color col, bool on) {
		try {
			var white = _hasWhiteChannel ? Math.Min(Math.Min(col.R, col.G), col.B) : 0;
			var gain = Math.Clamp(_brightness, 0, 100);
			var url = $"http://{IpAddress}/rpc/RGBW.Set?id=0&on={on.ToString().ToLower()}" +
			          $"&red={col.R}&green={col.G}&blue={col.B}&white={white}&gain={gain}";
			await HttpClient.GetAsync(url).ConfigureAwait(false);
		} catch (Exception e) {
			Log.Debug("Shelly: Exception sending RGBW: " + e.Message);
		}
	}

	private async Task TurnOff() {
		try {
			var url = $"http://{IpAddress}/rpc/RGBW.Set?id=0&on=false&red=0&green=0&blue=0&white=0&gain=0";
			await HttpClient.GetAsync(url).ConfigureAwait(false);
		} catch (Exception e) {
			Log.Debug("Shelly: Exception turning off: " + e.Message);
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
