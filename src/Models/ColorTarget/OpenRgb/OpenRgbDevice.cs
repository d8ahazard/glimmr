#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using OpenRGB.NET;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.OpenRgb {
	public class OpenRgbDevice : ColorTarget, IColorTarget {
		private OpenRgbData _data;
		private readonly ColorService _colorService;
		private OpenRGBClient? _client;
		public bool Streaming { get; set; }
		public bool Testing { get; set; }
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; }
		public bool Enable { get; set; }
		IColorTargetData IColorTarget.Data {
			get => _data;
			set => _data = (OpenRgbData) value;
		}

		public OpenRgbDevice(OpenRgbData data, ColorService cs) {
			Id = data.Id;
			_data = data;
			IpAddress = _data.IpAddress;
			Tag = _data.Tag;
			cs.ColorSendEvent += SetColor;
			_colorService = cs;
			_client = cs.ControlService.GetAgent("OpenRgbAgent");
			LoadData();
		}

		
		public Task StartStream(CancellationToken ct) {
			if (_client == null || !Enable) {
				return Task.CompletedTask;
			}

			if (!_client.Connected) {
				try {
					_client.Connect();
				} catch (Exception e) {
					Log.Debug("Exception connecting client: " + e.Message);
				}

				try {
					_client.Dispose();
					_client = _colorService.ControlService.GetAgent("OpenRgbAgent");
					_client?.Connect();
				} catch {
					// Ignored
				}
			}

			if (_client != null && _client.Connected) {
				Log.Information($"{_data.Tag}::Starting stream: {_data.Id}...");
				Streaming = true;
				_client.SetMode(_data.DeviceId, 0);
				Log.Information($"{_data.Tag}::Stream started: {_data.Id}.");
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

			_client.UpdateLeds(_data.DeviceId, output);
			await Task.FromResult(true);
			Streaming = false;
			Log.Information($"{_data.Tag}::Stream stopped: {_data.Id}.");
		}

		public void SetColor(List<Color> colors, List<Color> sectors, int fadeTime, bool force = false) {
			if (!Enable || !Streaming) {
				return;
			}

			var toSend = ColorUtil.TruncateColors(colors, _data.Offset, _data.LedCount);
			if (_data.Rotation == 180) {
				toSend = toSend.Reverse().ToArray();
			}

			var converted = toSend.Select(col => new OpenRGB.NET.Models.Color(col.R, col.G, col.B)).ToList();
			_client?.UpdateLeds(_data.DeviceId, converted.ToArray());

			_colorService.Counter.Tick(Id);
		}

		public Task FlashColor(Color color) {
			return Task.CompletedTask;
		}

		public Task ReloadData() {
			var dev = DataUtil.GetDevice(Id);
			if (dev != null) _data = dev; 
			return Task.CompletedTask;
		}

		public void Dispose() {
		}

		private void LoadData() {
			Enable = _data.Enable;
			IpAddress = _data.IpAddress;
		}
	}
}