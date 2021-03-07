using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Corsair.CUE.SDK;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Serilog;


namespace Glimmr.Models.ColorTarget.Corsair {
	public class CorsairDevice : ColorTarget, IColorTarget {
		public bool Streaming { get; set; }
		public bool Testing { get; set; }
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; }
		public bool Enable { get; set; }
		IColorTargetData IColorTarget.Data {
			get => Data;
			set => Data = (CorsairData) value;
		}

		private CorsairData Data { get; set; }
		
		private List<List<CorsairLedPosition>> _sortedPositions;

		private CorsairLedPositions _layout;
		

		public CorsairDevice(IColorTargetData data, ColorService colorService) : base(colorService) {
			try {
				Log.Debug("Creating corsair device...");
				Data = (CorsairData) data;
				Id = Data.Id;
				ReloadData();
				colorService.ColorSendEvent += SetColor;
				Log.Debug("Corsair device created...");

			} catch (Exception e) {
				Log.Debug("Exception: " + e.Message);
			}
			
		}
		public async Task StartStream(CancellationToken ct) {
			if (!Enable) return;
			Log.Debug("Enabling corsair stream...");
			if (!Streaming) Streaming = true;
			await Task.FromResult(true);
		}

		public async Task StopStream() {
			if (!Enable) return;
			await FlashColor(Color.FromArgb(0, 0, 0));
			if (Streaming) Streaming = false;
		}

		public void SetColor(List<Color> colors, List<Color> sectors, int fadeTime, bool force = false) {
			if (!Streaming || Testing && !force) {
				return;
			}
			var toSend = BuildColors(colors);
			//Log.Debug("Colors: " + JsonConvert.SerializeObject(toSend));
			CUESDK.CorsairSetLedsColorsBufferByDeviceIndex(Data.DeviceIndex, toSend.Count, toSend.ToArray());
			//Log.Debug($"Buffer {Data.DeviceIndex} set.");
			CUESDK.CorsairSetLedsColorsFlushBuffer();
			//Log.Debug("Flushing...");
		}
		

		public Task FlashColor(Color color) {
			var colors = ColorUtil.EmptyList(Data.LedCount, color);
			var toSend = BuildColors(colors);
			CUESDK.CorsairSetLedsColorsBufferByDeviceIndex(Data.DeviceIndex, toSend.Count, toSend.ToArray());
			CUESDK.CorsairSetLedsColorsFlushBuffer();
			return Task.CompletedTask;
		}

		public Task ReloadData() {
			Data = DataUtil.GetDevice<CorsairData>(Id);
			_layout = CUESDK.CorsairGetLedPositionsByDeviceIndex(Data.DeviceIndex);
			Enable = Data.Enable;
			BuildLayout();
			return Task.CompletedTask;
		}

		private void BuildLayout() {
			var count = _layout.numberOfLeds;
			var ordered = new Dictionary<double, List<CorsairLedPosition>>();
			// Loop over positions, sort by left value
			for (var i = 0; i < count; i++) {
				var ld = _layout.pLedPosition[i];
				var list = new List<CorsairLedPosition>();
				if (ordered.ContainsKey(ld.left)) {
					list = ordered[ld.left];
				}
				list.Add(ld);
				ordered[ld.left] = list;
			}
			// Sort entire list so it's left-right
			var foo = new Dictionary<double, List<CorsairLedPosition>>(ordered.OrderBy(o=>o.Key).ToList());
			_sortedPositions = foo.Values.ToList();
		}

		private List<CorsairLedColor> BuildColors(List<Color> colors) {
			var output = new List<CorsairLedColor>();
			var cData = ColorUtil.TruncateColors(colors, Data.Offset, _sortedPositions.Count);
			if (Data.Reverse) cData.Reverse();
			var i = 0;
			foreach (var pos in _sortedPositions) {
				var col = cData[i];
				foreach (var led in pos) {
					var nc = new CorsairLedColor {r = col.R, g = col.G, b = col.B, ledId = led.ledId};
					output.Add(nc);
				}

				i++;
			}

			return output;
		}
		
		public void Dispose() {
				
		}
	}
}