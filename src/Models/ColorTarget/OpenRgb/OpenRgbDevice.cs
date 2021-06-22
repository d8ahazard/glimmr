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

namespace Glimmr.Models.ColorTarget.OpenRgb {
	public class OpenRgbDevice : ColorTarget, IColorTarget {
		public OpenRgbData Data { get; set; }
		public bool Streaming { get; set; }
		public bool Testing { get; set; }
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; }
		public bool Enable { get; set; }

		private int _offset;
		private int _ledCount;

		private OpenRGBClient? _client;
		private readonly ColorService _colorService;
		


		public OpenRgbDevice(OpenRgbData data, ColorService cs) {
			Id = data.Id;
			Data = data;
			cs.ColorSendEvent += SetColor;
			_colorService = cs;
			_client = cs.ControlService.GetAgent("OpenRgbAgent");
			LoadData();
		}

		
		IColorTargetData IColorTarget.Data {
			get => Data;
			set => Data = (OpenRgbData) value;
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

				try
				{
					_client.Dispose();
					_client = _colorService.ControlService.GetAgent("OpenRgbAgent");
					_client.Connect();
				}
				catch
				{
					// Ignored
					
				}
			}

			if (_client.Connected) {
				Log.Information($"{Data.Tag}::Starting stream: {Data.Id}...");
				Streaming = true;
				_client.SetMode(Data.DeviceId, 0);
				Log.Information($"{Data.Tag}::Stream started: {Data.Id}.");
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

			var output = new OpenRGB.NET.Models.Color[Data.LedCount];
			for (var i = 0; i < output.Length; i++) {
				output[i] = new OpenRGB.NET.Models.Color();
			}

			_client.UpdateLeds(Data.DeviceId, output);
			await Task.FromResult(true);
			Streaming = false;
			Log.Information($"{Data.Tag}::Stream stopped: {Data.Id}.");
		}

		public void SetColor(List<Color> colors, List<Color> sectors, int fadeTime, bool force = false) {
			if (!Enable || !Streaming) {
				return;
			}

			var toSend = ColorUtil.TruncateColors(colors, Data.Offset, Data.LedCount);
			if (Data.Rotation == 180) {
				toSend = toSend.Reverse().ToArray();
			}

			var converted = toSend.Select(col => new OpenRGB.NET.Models.Color(col.R, col.G, col.B)).ToList();
			_client.UpdateLeds(Data.DeviceId, converted.ToArray());
		
			_colorService.Counter.Tick(Id);
		}

		public Task FlashColor(Color color) {
			return Task.CompletedTask;
		}

		public Task ReloadData() {
			Data = DataUtil.GetDevice(Id);
			return Task.CompletedTask;
		}

		private void LoadData() {
			Enable = Data.Enable;
			IpAddress = Data.IpAddress;
			_offset = Data.Offset;
			_ledCount = Data.LedCount;
			if (_client != null) {
				var dev = _client.GetControllerData(Data.DeviceId);
				_ledCount = dev.Leds.Length;
			}
			
		}

		public void Dispose() {
		}
	}
}