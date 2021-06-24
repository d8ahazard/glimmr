#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using rpi_ws281x;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Led {
	public class LedDevice : ColorTarget, IColorTarget {
		private float CurrentMilliamps { get; set; }
		private readonly Controller? _controller;
		private readonly int _controllerId;
		private readonly WS281x? _ws;
		private LedData _data;
		private readonly LedAgent? _agent;
		private bool _enableAbl;

		private int _ledCount;
		private int _offset;


		public LedDevice(LedData ld, ColorService colorService) : base(colorService) {
			_data = ld;
			Id = _data.Id;
			_controllerId = int.Parse(Id);
			IpAddress = _data.IpAddress;
			Tag = _data.Tag;

			var cs = colorService;
			_agent = cs.ControlService.GetAgent("LedAgent");
			if (_agent == null) return;
			_ws = _agent.Ws281X;
			if (_ws == null) return;
			cs.ColorSendEvent += SetColor;
			if (Id == "0") {
				_controller = _ws.GetController();
			}

			if (Id == "1") {
				_controller = _ws.GetController(ControllerType.PWM1);
			}

			ReloadData();
		}

		public bool Streaming { get; set; }
		public bool Testing { get; set; }
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; }
		public bool Enable { get; set; }


		IColorTargetData IColorTarget.Data {
			get => _data;
			set => _data = (LedData) value;
		}

		public async Task StartStream(CancellationToken ct) {
			if (!Enable) {
				return;
			}

			Log.Information($"{_data.Tag}::Starting stream: {_data.Id}...");
			Streaming = true;
			await Task.FromResult(Streaming);
			Log.Information($"{_data.Tag}::Stream started: {_data.Id}.");
		}

		public async Task StopStream() {
			if (!Streaming) {
				return;
			}

			Log.Information($"{_data.Tag}::Stopping stream. {_data.Id}.");
			await StopLights().ConfigureAwait(false);
			Log.Information($"{_data.Tag}::Stream stopped: {_data.Id}.");
			Streaming = false;
		}


		public Task FlashColor(Color color) {
			_controller?.SetAll(color);
			return Task.CompletedTask;
		}


		public void Dispose() {
			_controller?.Reset();
		}

		public Task ReloadData() {
			var ld = DataUtil.GetDevice<LedData>(Id);
			var sd = DataUtil.GetSystemData();

			if (ld == null) {
				Log.Warning("No LED Data");
				return Task.CompletedTask;
			}

			_data = ld;
			Enable = _data.Enable;
			_agent?.ToggleStrip(_controllerId, Enable);
			_ledCount = _data.LedCount;
			if (_ledCount > sd.LedCount) {
				_ledCount = sd.LedCount;
			}

			_enableAbl = _data.AutoBrightnessLevel;
			_offset = _data.Offset;

			if (!Enable) {
				return Task.CompletedTask;
			}

			if (_data.Brightness != ld.Brightness && !_enableAbl) {
				_ws?.SetBrightness(ld.Brightness / 100 * 255, _controllerId);
			}

			if (_data.LedCount != ld.LedCount) {
				_ws?.SetBrightness(ld.LedCount, _controllerId);
			}

			return Task.CompletedTask;
		}

		public void SetColor(List<Color> colors, List<Color> sectors, int fadeTime, bool force = false) {
			if (colors == null) {
				throw new ArgumentException("Invalid color input.");
			}

			if (!Streaming || !Enable || Testing && !force) {
				return;
			}

			var c1 = ColorUtil.TruncateColors(colors, _offset, _ledCount);
			if (_enableAbl) {
				c1 = VoltAdjust(c1, _data);
			}

			for (var i = 0; i < c1.Length; i++) {
				var tCol = c1[i];
				if (_data.StripType == 1) {
					tCol = ColorUtil.ClampAlpha(tCol);
				}

				_controller?.SetLED(i, tCol);
			}

			_agent?.Update(_controllerId);
			ColorService?.Counter.Tick(Id);
		}


		private async Task StopLights() {
			if (!Enable) {
				return;
			}

			for (var i = 0; i < _ledCount; i++) {
				_controller?.SetLED(i, Color.FromArgb(0, 0, 0, 0));
			}

			_agent?.Update(_controllerId);
			await Task.FromResult(true);
		}

		private Color[] VoltAdjust(Color[] input, LedData ld) {
			//power limit calculation
			//each LED can draw up 195075 "power units" (approx. 53mA)
			//one PU is the power it takes to have 1 channel 1 step brighter per brightness step
			//so A=2,R=255,G=0,B=0 would use 510 PU per LED (1mA is about 3700 PU)
			var actualMilliampsPerLed = ld.MilliampsPerLed; // 20
			var defaultBrightness = (int) (ld.Brightness / 100f * 255);
			var ablMaxMilliamps = ld.AblMaxMilliamps; // 4500
			var length = input.Length;
			var output = input;
			if (ablMaxMilliamps > 149 && actualMilliampsPerLed > 0) {
				//0 mA per LED and too low numbers turn off calculation

				var puPerMilliamp = 195075 / actualMilliampsPerLed;
				var powerBudget = ablMaxMilliamps * puPerMilliamp; //100mA for ESP power
				if (powerBudget > puPerMilliamp * length) {
					//each LED uses about 1mA in standby, exclude that from power budget
					powerBudget -= puPerMilliamp * length;
				} else {
					powerBudget = 0;
				}

				var powerSum = 0;

				for (var i = 0; i < length; i++) {
					//sum up the usage of each LED
					var c = input[i];
					powerSum += c.R + c.G + c.B + c.A;
				}

				if (ld.StripType == 1) {
					//RGBW led total output with white LEDs enabled is still 50mA, so each channel uses less
					powerSum *= 3;
					powerSum >>= 2; //same as /= 4
				}

				var powerSum0 = powerSum;
				powerSum *= defaultBrightness;

				if (powerSum > powerBudget) {
					//scale brightness down to stay in current limit
					var scale = powerBudget / (float) powerSum;
					var scaleI = scale * 255;
					var scaleB = scaleI > 255 ? 255 : scaleI;
					var newBri = scale8(defaultBrightness, scaleB);
					_ws?.SetBrightness((int) newBri, _controllerId);
					CurrentMilliamps = powerSum0 * newBri / puPerMilliamp;
					if (newBri < defaultBrightness) {
						//output = ColorUtil.ClampBrightness(input, newBri);
					}
				} else {
					CurrentMilliamps = (float) powerSum / puPerMilliamp;
					if (defaultBrightness < 255) {
						_ws?.SetBrightness(defaultBrightness, _controllerId);
					}
				}

				CurrentMilliamps += length; //add standby power back to estimate
			} else {
				CurrentMilliamps = 0;
				if (defaultBrightness < 255) {
					_ws?.SetBrightness(defaultBrightness, _controllerId);
				}
			}

			return output;
		}

		private float scale8(float i, float scale) {
			return i * (scale / 256);
		}
	}
}