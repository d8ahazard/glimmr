#region

using System;
using System.Collections.Generic;
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

	private int _ledCount;
	private float _multiplier;
	private int _offset;
	private StripMode _stripMode;
	private int _targetSector;

	public OpenRgbDevice(OpenRgbData data, ColorService cs) : base(cs) {
		Id = data.Id;
		_data = data;
		_client = cs.ControlService.GetAgent("OpenRgbAgent");
		_multiplier = _data.LedMultiplier;
		ReloadData();
		ColorService.ColorSendEventAsync += SetColors;
	}

	public bool Streaming { get; set; }

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
		bool connected;
		try {
			var mt = new OpenRGB.NET.Models.Color[_data.LedCount];
			for (var i = 0; i < mt.Length; i++) {
				mt[i] = new OpenRGB.NET.Models.Color();
			}

			connected = _client.SetMode(_data.DeviceId, 0);
		} catch (Exception e) {
			Log.Warning("Exception setting mode..." + e.Message);
			return Task.CompletedTask;
		}

		if (connected) {
			Streaming = true;
			Log.Debug($"{_data.Tag}::Stream started: {_data.Id}.");
		} else {
			Log.Debug($"{_data.Tag}::Stream start failed: {_data.Id}.");
		}

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


	public void Dispose() { }


	public async Task SetColors(IReadOnlyList<Color> list, IReadOnlyList<Color> colors1) {
		if (!Streaming || !Enable) {
			return;
		}

		var toSend = list.ToArray();
		switch (_stripMode) {
			case StripMode.Single when _targetSector > colors1.Count || _targetSector == -1:
				return;
			case StripMode.Single:
				toSend = ColorUtil.FillArray(colors1[_targetSector - 1], _ledCount);
				break;
			case StripMode.Normal:
			case StripMode.Sectored:
			case StripMode.Loop:
			default: {
				toSend = ColorUtil.TruncateColors(toSend, _offset, _ledCount, _multiplier);
				if (_stripMode == StripMode.Loop) {
					toSend = ShiftColors(toSend);
				} else {
					if (_data.ReverseStrip) {
						toSend = toSend.Reverse().ToArray();
					}
				}

				break;
			}
		}


		List<OpenRGB.NET.Models.Color>? converted;
		switch (_data.ColorOrder) {
			case ColorOrder.Rgb:
				converted = toSend.Select(col => new OpenRGB.NET.Models.Color(col.R, col.G, col.B)).ToList();
				break;
			case ColorOrder.Rbg:
				converted = toSend.Select(col => new OpenRGB.NET.Models.Color(col.R, col.B, col.G)).ToList();
				break;
			case ColorOrder.Gbr:
				converted = toSend.Select(col => new OpenRGB.NET.Models.Color(col.G, col.B, col.R)).ToList();
				break;
			case ColorOrder.Grb:
				converted = toSend.Select(col => new OpenRGB.NET.Models.Color(col.G, col.R, col.B)).ToList();
				break;
			case ColorOrder.Bgr:
				converted = toSend.Select(col => new OpenRGB.NET.Models.Color(col.B, col.G, col.R)).ToList();
				break;
			case ColorOrder.Brg:
				converted = toSend.Select(col => new OpenRGB.NET.Models.Color(col.B, col.R, col.G)).ToList();
				break;
			default:
				Log.Debug("Well, this is odd...");
				converted = toSend.Select(col => new OpenRGB.NET.Models.Color(col.R, col.B, col.G)).ToList();
				break;
		}

		_client?.Update(_data.DeviceId, converted.ToArray());
		await Task.FromResult(true);
	}

	public Task ReloadData() {
		var dev = DataUtil.GetDevice<OpenRgbData>(Id);
		if (dev == null) {
			return Task.CompletedTask;
		}

		_data = dev;
		Enable = _data.Enable;


		_offset = _data.Offset;
		Enable = _data.Enable;
		_stripMode = _data.StripMode;
		_targetSector = _data.TargetSector;
		_multiplier = _data.LedMultiplier;
		if (_multiplier == 0) {
			_multiplier = 1;
		}

		_ledCount = _data.LedCount;
		return Task.CompletedTask;
	}

	private Task SetColors(object sender, ColorSendEventArgs args) {
		if (!_client?.Connected ?? false) {
			return Task.CompletedTask;
		}

		return SetColors(args.LedColors, args.SectorColors);
	}

	private Color[] ShiftColors(IReadOnlyList<Color> input) {
		var output = new Color[input.Count];
		var il = output.Length - 1;
		if (!_data.ReverseStrip) {
			for (var i = 0; i < input.Count / 2; i++) {
				output[i] = input[i];
				output[il - i] = input[i];
			}
		} else {
			var l = 0;
			for (var i = (input.Count - 1) / 2; i >= 0; i--) {
				output[i] = input[l];
				output[il - i] = input[l];
				l++;
			}
		}

		return output;
	}
}