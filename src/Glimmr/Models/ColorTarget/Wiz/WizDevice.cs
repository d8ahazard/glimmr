#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Wiz;

public class WizDevice : ColorTarget, IColorTarget, IDisposable {
	private string IpAddress { get; set; }
	private const int WizPort = 38899;

	private readonly UdpClient _udpClient;
	private WizData _data;
	private bool _disposed;
	private IPEndPoint? _ep;
	private int _brightness;
	private int _targetSector;

	public WizDevice(WizData wd, ColorService cs) : base(cs) {
		_data = wd ?? throw new ArgumentException("Invalid WiZ data.");
		Id = _data.Id;
		IpAddress = _data.IpAddress;
		_brightness = _data.Brightness;
		_udpClient = cs.ControlService.UdpClient;
		cs.ControlService.RefreshSystemEvent += RefreshSystem;
		ReloadData();
		cs.ColorSendEventAsync += SetColors;
	}

	public bool Enable { get; set; }
	public bool Streaming { get; set; }
	public string Id { get; }

	IColorTargetData IColorTarget.Data {
		get => _data;
		set => _data = (WizData)value;
	}

	public async Task StartStream(CancellationToken ct) {
		if (Streaming || !Enable) return;

		Log.Debug($"{_data.Tag}::Starting stream: {_data.Id}...");
		_ep = IpUtil.Parse(IpAddress, WizPort);
		if (_ep == null) return;

		await SendCommand(new { method = "setPilot", @params = new { state = true } });
		Streaming = true;
		Log.Debug($"{_data.Tag}::Stream started: {_data.Id}.");
	}

	public async Task StopStream() {
		if (!Streaming) return;

		Log.Debug($"{_data.Tag}::Stopping stream...{_data.Id}.");
		Streaming = false;
		await FlashColor(Color.Black);
		await SendCommand(new { method = "setPilot", @params = new { state = false } });
		Log.Debug($"{_data.Tag}::Stream stopped: {_data.Id}.");
	}

	public async Task FlashColor(Color color) {
		if (_ep == null) return;
		await SendColor(color);
	}

	public Task ReloadData() {
		var dev = DataUtil.GetDevice<WizData>(Id);
		if (dev != null) _data = dev;
		_brightness = _data.Brightness;
		_targetSector = _data.TargetSector;
		IpAddress = _data.IpAddress;
		Enable = _data.Enable;
		return Task.CompletedTask;
	}

	public async Task SetColors(IReadOnlyList<Color> ledColors, IReadOnlyList<Color> sectorColors) {
		if (!Streaming || !Enable || _ep == null) return;

		if (_targetSector <= 0 || _targetSector > sectorColors.Count) return;

		var col = sectorColors[_targetSector - 1];
		if (_brightness < 100) {
			col = ColorUtil.ClampBrightness(col, (int)(_brightness * 2.55f));
		}

		await SendColor(col);
	}

	private Task SetColors(object sender, ColorSendEventArgs args) {
		return SetColors(args.LedColors, args.SectorColors);
	}

	private async Task SendColor(Color col) {
		var dimming = Math.Max(10, _brightness);
		await SendCommand(new {
			method = "setPilot",
			@params = new { r = (int)col.R, g = (int)col.G, b = (int)col.B, dimming }
		});
	}

	private async Task SendCommand(object command) {
		if (_ep == null) return;
		try {
			var json = JsonConvert.SerializeObject(command);
			var bytes = Encoding.UTF8.GetBytes(json);
			await _udpClient.SendAsync(bytes, bytes.Length, _ep).ConfigureAwait(false);
		} catch (Exception e) {
			Log.Debug("WiZ: Exception sending command: " + e.Message);
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
