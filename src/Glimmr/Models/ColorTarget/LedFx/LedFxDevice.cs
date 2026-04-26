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
using Newtonsoft.Json.Linq;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.LedFx;

public class LedFxDevice : ColorTarget, IColorTarget, IDisposable {
	private static readonly HttpClient HttpClient = new() {
		Timeout = TimeSpan.FromSeconds(5)
	};

	private LedFxData _data;
	private bool _disposed;
	private List<string> _deviceIds = new();

	public LedFxDevice(LedFxData ld, ColorService cs) : base(cs) {
		cs.ControlService.RefreshSystemEvent += RefreshSystem;
		_data = ld ?? throw new ArgumentException("Invalid LedFx data.");
		Id = _data.Id;
		ReloadData();
		ColorService.ColorSendEventAsync += SetColors;
	}

	public bool Enable { get; set; }
	public bool Streaming { get; set; }
	public string Id { get; }

	IColorTargetData IColorTarget.Data {
		get => _data;
		set => _data = (LedFxData)value;
	}

	public async Task StartStream(CancellationToken ct) {
		if (Streaming || !Enable) return;

		Log.Debug("{Tag}::Starting stream: {Id}...", _data.Tag, _data.Id);
		await FetchDeviceIds();
		Streaming = true;
		Log.Debug("{Tag}::Stream started: {Id}.", _data.Tag, _data.Id);
	}

	public Task StopStream() {
		if (!Streaming) return Task.CompletedTask;

		Log.Debug("{Tag}::Stopping stream: {Id}...", _data.Tag, _data.Id);
		Streaming = false;
		Log.Debug("{Tag}::Stream stopped: {Id}.", _data.Tag, _data.Id);
		return Task.CompletedTask;
	}

	public Task FlashColor(Color color) {
		return Task.CompletedTask;
	}

	public Task SetColors(IReadOnlyList<Color> ledColors, IReadOnlyList<Color> sectorColors) {
		return Task.CompletedTask;
	}

	public Task ReloadData() {
		var dev = DataUtil.GetDevice<LedFxData>(Id);
		if (dev != null) {
			_data = dev;
		}

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

	private string BaseUrl => $"http://{_data.IpAddress}:{_data.Port}";

	private async Task FetchDeviceIds() {
		_deviceIds.Clear();
		try {
			var response = await HttpClient.GetAsync($"{BaseUrl}/api/devices");
			if (!response.IsSuccessStatusCode) return;

			var body = await response.Content.ReadAsStringAsync();
			var obj = JObject.Parse(body);
			var devices = obj.SelectToken("devices") as JObject;
			if (devices == null) return;

			foreach (var prop in devices.Properties()) {
				_deviceIds.Add(prop.Name);
			}

			Log.Debug("LedFx: Found {Count} devices on {Ip}", _deviceIds.Count, _data.IpAddress);
		} catch (Exception ex) {
			Log.Debug("LedFx: Error fetching devices: " + ex.Message);
		}
	}

	/// <summary>
	///     Trigger a named effect on a specific LedFx device.
	/// </summary>
	public async Task TriggerEffect(string ledfxDeviceId, string effectType, object? effectConfig = null) {
		try {
			var payload = new {
				type = effectType,
				config = effectConfig ?? new { }
			};

			var json = JsonConvert.SerializeObject(payload);
			using var content = new StringContent(json, Encoding.UTF8, "application/json");
			await HttpClient.PostAsync($"{BaseUrl}/api/devices/{ledfxDeviceId}/effects", content)
				.ConfigureAwait(false);
		} catch (Exception ex) {
			Log.Debug("LedFx: Error triggering effect: " + ex.Message);
		}
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
