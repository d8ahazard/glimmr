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
		public bool Streaming { get; set; }
		public bool Testing { get; set; }
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; }
		public bool Enable { get; set; }
		public bool Online { get; set; }
		private readonly ColorService _colorService;
		private readonly OpenRGBClient _client;
		private List<List<Color>> _frameBuffer;
		private int _frameDelay;
		
		IColorTargetData IColorTarget.Data {
			get => Data;
			set => Data = (OpenRgbData) value;
		}

		public OpenRgbData Data { get; set; }

		
		public OpenRgbDevice(OpenRgbData data, ColorService cs) {
			Data = data;
			Id = Data.Id;
			Enable = data.Enable;
			cs.ColorSendEvent += SetColor;
			_colorService = cs;
			_client = cs.ControlService.GetAgent("OpenRgbAgent");
		}
		
		public Task StartStream(CancellationToken ct) {
			if (_client == null || !Online) return Task.CompletedTask;
			
			if (_client.Connected && Enable) {
				Log.Information("OpenRGB: Starting stream...");
				Streaming = true;
				_client.SetMode(Data.DeviceId,0);
				Log.Information("OpenRGB: Stream started.");
				_frameBuffer = new List<List<Color>>();
			}
			return Task.CompletedTask;
		}

		public Task StopStream() {
			if (_client == null) return Task.CompletedTask;
			var output = new OpenRGB.NET.Models.Color[Data.LedCount];
			for (var i = 0; i < output.Length; i++) {
				output[i] = new OpenRGB.NET.Models.Color();
			}
			_client.UpdateLeds(Data.DeviceId,output);
			Streaming = false;
			return Task.CompletedTask;
		}

		public void SetColor(List<Color> colors, List<Color> sectors, int fadeTime, bool force = false) {
			if (!Enable || !Streaming) {
				return;
			} 
			var toSend = ColorUtil.TruncateColors(colors, Data.Offset, Data.LedCount);
			if (Data.Rotation == 180) toSend.Reverse();
			
			if (_frameDelay > 0) {
				_frameBuffer.Add(toSend);
				if (_frameBuffer.Count < _frameDelay) return; // Just buffer till we reach our count
				toSend = _frameBuffer[0];
				_frameBuffer.RemoveAt(0);	
			}

			var converted = toSend.Select(col => new OpenRGB.NET.Models.Color(col.R, col.G, col.B)).ToList();
			_client.UpdateLeds(Data.DeviceId,converted.ToArray());
			_colorService.Counter.Tick(Id);
		}

		public Task FlashColor(Color color) {
			return Task.CompletedTask;
		}

		public Task ReloadData() {
			Data = DataUtil.GetDevice(Id);
			Enable = Data.Enable;
			_frameDelay = Data.FrameDelay;
			_frameBuffer = new List<List<Color>>();
			Online = SystemUtil.IsOnline(Data.IpAddress);
			return Task.CompletedTask;
		}

		public void Dispose() {
			return;
		}
	}
}