#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.RazerChroma;

public class RazerChromaDevice : ColorTarget, IColorTarget, IDisposable {
	private static readonly HttpClient HttpClient = new();

	private RazerChromaData _data;
	private string _sessionUrl;
	private int _brightness;
	private int _targetSector;
	private bool _disposed;
	private CancellationTokenSource? _heartbeatCts;

	public RazerChromaDevice(RazerChromaData data, ColorService cs) : base(cs) {
		_data = data ?? throw new ArgumentException("Invalid Razer Chroma data.");
		Id = _data.Id;
		_sessionUrl = _data.SessionUrl;
		_brightness = _data.Brightness;
		_targetSector = _data.TargetSector;
		cs.ControlService.RefreshSystemEvent += RefreshSystem;
		ReloadData();
		cs.ColorSendEventAsync += SetColors;
	}

	public bool Enable { get; set; }
	public bool Streaming { get; set; }
	public string Id { get; }

	IColorTargetData IColorTarget.Data {
		get => _data;
		set => _data = (RazerChromaData)value;
	}

	public Task StartStream(CancellationToken ct) {
		if (Streaming || !Enable || string.IsNullOrEmpty(_sessionUrl)) {
			return Task.CompletedTask;
		}

		Log.Debug($"{_data.Tag}::Starting stream: {_data.Id}...");
		_heartbeatCts = new CancellationTokenSource();
		_ = HeartbeatLoop(_heartbeatCts.Token);
		Streaming = true;
		Log.Debug($"{_data.Tag}::Stream started: {_data.Id}.");
		return Task.CompletedTask;
	}

	public async Task StopStream() {
		if (!Streaming) {
			return;
		}

		Log.Debug($"{_data.Tag}::Stopping stream: {_data.Id}...");
		Streaming = false;
		_heartbeatCts?.Cancel();
		_heartbeatCts?.Dispose();
		_heartbeatCts = null;

		try {
			await HttpClient.DeleteAsync(_sessionUrl);
		} catch (Exception e) {
			Log.Debug("RazerChroma: Error deleting session - " + e.Message);
		}

		Log.Debug($"{_data.Tag}::Stream stopped: {_data.Id}.");
	}

	public async Task FlashColor(Color color) {
		await SendChromaLink(color);
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

		var scale = _brightness / 100f;
		var scaled = Color.FromArgb(
			(int)(color.R * scale),
			(int)(color.G * scale),
			(int)(color.B * scale));
		await SendChromaLink(scaled);
	}

	public Task ReloadData() {
		var dev = DataUtil.GetDevice<RazerChromaData>(Id);
		if (dev != null) {
			_data = dev;
		}

		_sessionUrl = _data.SessionUrl;
		_brightness = _data.Brightness;
		_targetSector = _data.TargetSector;
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

	private static int ToBgr(Color c) {
		return (c.B << 16) | (c.G << 8) | c.R;
	}

	private async Task SendChromaLink(Color color) {
		if (string.IsNullOrEmpty(_sessionUrl)) {
			return;
		}

		try {
			var bgr = ToBgr(color);
			var param = new[] { bgr, bgr, bgr, bgr, bgr };
			var body = new {
				effect = "CHROMA_CUSTOM",
				param = new[] { param }
			};

			var json = Newtonsoft.Json.JsonConvert.SerializeObject(body);
			using var content = new StringContent(json, Encoding.UTF8, "application/json");
			using var request = new HttpRequestMessage(HttpMethod.Put, _sessionUrl + "/chromalink");
			request.Content = content;
			await HttpClient.SendAsync(request);
		} catch (Exception e) {
			Log.Debug("RazerChroma: Error sending ChromaLink - " + e.Message);
		}
	}

	private async Task HeartbeatLoop(CancellationToken ct) {
		while (!ct.IsCancellationRequested) {
			try {
				using var request = new HttpRequestMessage(HttpMethod.Put, _sessionUrl + "/heartbeat");
				await HttpClient.SendAsync(request, ct);
				await Task.Delay(1000, ct);
			} catch (OperationCanceledException) {
				break;
			} catch (Exception e) {
				Log.Debug("RazerChroma: Heartbeat error - " + e.Message);
				break;
			}
		}
	}

	protected virtual void Dispose(bool disposing) {
		if (_disposed) {
			return;
		}

		if (disposing) {
			_heartbeatCts?.Cancel();
			_heartbeatCts?.Dispose();
			if (Streaming) {
				StopStream().ConfigureAwait(false);
			}
		}

		_disposed = true;
	}
}
