#region

using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.OpenRgb {
	public class OpenRgbDevice : ColorTarget, IColorTarget {
		private readonly ColorService _colorService;
		private OpenRgbAgent? _client;
		private OpenRgbData _data;

		public OpenRgbDevice(OpenRgbData data, ColorService cs) {
			Id = data.Id;
			_data = data;
			_colorService = cs;
			_client = cs.ControlService.GetAgent("OpenRgbAgent");
			LoadData();
			_colorService.ColorSendEventAsync += SetColors;
		}

		public bool Streaming { get; set; }

		public bool Testing { private get; set; }
		public string Id { get; }
		public bool Enable { get; set; }

		IColorTargetData IColorTarget.Data {
			get => _data;
			set => _data = (OpenRgbData) value;
		}

		public Task StartStream(CancellationToken ct) {
			if (_client == null) {
				_client = _colorService.ControlService.GetAgent("OpenRgbAgent");
			}
			if (_client == null || !Enable) {
				if (_client == null) Log.Debug("Null client, returning.");
				return Task.CompletedTask;
			}

			_client.Connect();
			if (_client.Connected) {
				Log.Debug($"{_data.Tag}::Starting stream: {_data.Id}...");
				try {
					var mt = new OpenRGB.NET.Models.Color[_data.LedCount];
					for (var i = 0; i < mt.Length; i++) {
						mt[i] = new OpenRGB.NET.Models.Color();
					}
					_client.SetMode(_data.DeviceId, 0, mt);
				} catch (Exception e) {
					Log.Warning("Exception setting mode..." + e.Message);
					return Task.CompletedTask;
				}

				Streaming = true;
				Log.Debug($"{_data.Tag}::Stream started: {_data.Id}.");
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

		public Task ReloadData() {
			Log.Debug("Reloading data...");
			var dev = DataUtil.GetDevice(Id);
			if (dev != null) {
				_data = dev;
				Enable = _data.Enable;
				
			}

			return Task.CompletedTask;
		}

		public void Dispose() {
		}

		private async Task SetColors(object sender, ColorSendEventArgs args) {
			if (!_client?.Connected ?? false) return;
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

			var converted = toSend.Select(col => new OpenRGB.NET.Models.Color(col.R, col.G, col.B)).ToList();
			_client?.Update(_data.DeviceId, converted.ToArray());
			await Task.FromResult(true);
			_colorService.Counter.Tick(Id);
		}

		private void LoadData() {
			Enable = _data.Enable;
		}
	}
}