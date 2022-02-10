#region

using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.OpenRgb;

public class OpenRgbDevice : ColorTarget, IColorTarget {
	private OpenRgbAgent? _client;
	private OpenRgbData _data;

	public OpenRgbDevice(OpenRgbData data, ColorService cs) : base(cs) {
		Id = data.Id;
		_data = data;
		_client = cs.ControlService.GetAgent("OpenRgbAgent");
		LoadData();
		ColorService.ColorSendEventAsync += SetColors;
	}

	public bool Streaming { get; set; }

	public bool Testing { private get; set; }
	public string Id { get; }
	public bool Enable { get; set; }

	IColorTargetData IColorTarget.Data {
		get => _data;
		set => _data = (OpenRgbData)value;
	}

	public Task StartStream(CancellationToken ct) {
		_client ??= ColorService.ControlService.GetAgent("OpenRgbAgent");
		if (_client == null || !Enable) {
			if (_client == null) {
				Log.Debug("Null client, returning.");
			}

			return Task.CompletedTask;
		}

		if (!_client.Connected) {
			return Task.CompletedTask;
		}

		Log.Debug($"{_data.Tag}::Starting stream: {_data.Id}...");
		ColorService.StartCounter++;
		bool connected;
		try {
			var mt = new OpenRGB.NET.Models.Color[_data.LedCount];
			for (var i = 0; i < mt.Length; i++) {
				mt[i] = new OpenRGB.NET.Models.Color();
			}

			connected = _client.SetMode(_data.DeviceId, 0);
		} catch (Exception e) {
			Log.Warning("Exception setting mode..." + e.Message);
			ColorService.StartCounter--;
			return Task.CompletedTask;
		}

		if (connected) {
			Streaming = true;
			Log.Debug($"{_data.Tag}::Stream started: {_data.Id}.");
		} else {
			Log.Debug($"{_data.Tag}::Stream start failed: {_data.Id}.");
		}

		ColorService.StartCounter--;
		return Task.CompletedTask;
	}

	public async Task StopStream() {
		if (_client == null) {
			return;
		}

		if (!_client.Connected || !Streaming) {
			return;
		}

		var output = new OpenRGB.NET.Models.Color[_data.LedCount];
		for (var i = 0; i < output.Length; i++) {
			output[i] = new OpenRGB.NET.Models.Color();
		}

		_client.Update(_data.DeviceId, output);
		_client.Update(_data.DeviceId, output);
		_client.Update(_data.DeviceId, output);
		await Task.FromResult(true);
		Streaming = false;
		Log.Debug($"{_data.Tag}::Stream stopped: {_data.Id}.");
	}

	public Task FlashColor(Color color) {
		return Task.CompletedTask;
	}

	public Task ReloadData() {
		var dev = DataUtil.GetDevice(Id);
		if (dev == null) {
			return Task.CompletedTask;
		}

		_data = dev;
		Enable = _data.Enable;

		return Task.CompletedTask;
	}

	public void Dispose() {
	}

	private async Task SetColors(object sender, ColorSendEventArgs args) {
		if (!_client?.Connected ?? false) {
			return;
		}

		await SetColor(args.LedColors, args.Force).ConfigureAwait(false);
	}


	private async Task SetColor(Color[] colors, bool force = false) {
		if (!Enable || !Streaming || Testing && !force) {
			return;
		}

		var toSend = ColorUtil.TruncateColors(colors, _data.Offset, _data.LedCount);
		if (_data.Rotation == 180) {
			toSend = toSend.Reverse().ToArray();
		}

		var converted = _data.ColorOrder switch {
			ColorOrder.Rgb => toSend.Select(col => new OpenRGB.NET.Models.Color(col.R, col.G, col.B)).ToList(),
			ColorOrder.Rbg => toSend.Select(col => new OpenRGB.NET.Models.Color(col.R, col.B, col.G)).ToList(),
			ColorOrder.Gbr => toSend.Select(col => new OpenRGB.NET.Models.Color(col.G, col.B, col.R)).ToList(),
			ColorOrder.Grb => toSend.Select(col => new OpenRGB.NET.Models.Color(col.G, col.R, col.B)).ToList(),
			ColorOrder.Bgr => toSend.Select(col => new OpenRGB.NET.Models.Color(col.B, col.G, col.R)).ToList(),
			ColorOrder.Brg => toSend.Select(col => new OpenRGB.NET.Models.Color(col.B, col.R, col.G)).ToList(),
			_ => throw new ArgumentOutOfRangeException(nameof(_data.ColorOrder))
		};

		_client?.Update(_data.DeviceId, converted.ToArray());
		await Task.FromResult(true);
		ColorService.Counter.Tick(Id);
	}

	private void LoadData() {
		Enable = _data.Enable;
	}
}