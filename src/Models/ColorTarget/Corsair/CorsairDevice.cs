using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Corsair.CUE.SDK;
using Glimmr.Models.Util;
using Glimmr.Services;
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
		public IColorTargetData Data { get; set; }
		
		private int _bottomStart;
		private int _bottomCount;
		

		public CorsairDevice(IColorTargetData data, ColorService colorService) : base(colorService) {
			try {
				ReloadData();
				colorService.ColorSendEvent += SetColor;

			} catch (Exception e) {
				Log.Debug("Exception: " + e.Message);
			}
			
		}
		public Task StartStream(CancellationToken ct) {
			if (!Streaming) Streaming = true;
			return Task.CompletedTask;
		}

		public Task StopStream() {
			if (Streaming) Streaming = false;
			return Task.CompletedTask;
		}

		public void SetColor(List<Color> colors, List<Color> sectors, int fadeTime, bool force = false) {
			if (!Streaming || Testing && !force) return;
			
		}

		public void UpdateKeyboard(List<Color> colors) {
			var source = ColorUtil.TruncateColors(colors, 0, 10);
		}

		public void UpdateMouse(List<Color> colors) {
			
		}

		public void UpdateMouseMat(List<Color> colors) {
			
		}
		
		public void UpdateHeadset(List<Color> colors) {
			
		}
		
		public void UpdateHeadsetStand(List<Color> colors) {
			
		}

		public Task FlashColor(Color color) {
			return Task.CompletedTask;
		}

		public Task ReloadData() {
			var devs = CUESDK.CorsairGetDeviceCount();
			if (devs > 0) {
				for (var i = 0; i < devs; i++) {
					var info = CUESDK.CorsairGetDeviceInfo(i);
					
					var layout = CUESDK.CorsairGetLedPositionsByDeviceIndex(i);
					//_devices[info.type] = layout;
				}
			}
			SystemData sd = DataUtil.GetObject<SystemData>("SystemData");
			_bottomStart = sd.RightCount + sd.LeftCount + sd.TopCount;
			_bottomCount = sd.BottomCount;
			return Task.CompletedTask;
		}

		public void Dispose() {
			
		}
	}
}