using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using rpi_ws281x;
using Serilog;
using ColorUtil = Glimmr.Models.Util.ColorUtil;

namespace Glimmr.Models.ColorTarget.LED {
	public class LedDevice : ColorTarget, IColorTarget {
		public bool Streaming { get; set; }
		public bool Testing { get; set; }
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; }
		public bool Enable { get; set; }
		IColorTargetData IColorTarget.Data {
			get => Data;
			set => Data = (LedData) value;
		}
		public float CurrentMilliamps { get; set; }
		public LedData Data;
		
		private int _ledCount;
		private int _offset;
		private bool _enableAbl;
		private WS281x _strip;

		
		public LedDevice(LedData ld, ColorService colorService) : base(colorService) {
			var cs = colorService;
			cs.ColorSendEvent += SetColor;
			Data = ld;
			Id = Data.Id;
			ReloadData();
			CreateStrip();
		}
		

		
		private void CreateStrip() {
			var settings = LoadLedData(Data);
			// Hey, look, this is built natively into the LED app
			try {
				_strip = new WS281x(settings);
				Streaming = true;
				Log.Debug($@"Strip created using {_ledCount} LEDs.");
			} catch (Exception) {
				Log.Debug("Unable to initialize strips, or something.");
				Streaming = false;
			}
		}

		public Task StartStream(CancellationToken ct) {
			Log.Debug("Starting LED stream...");
			if (!Enable || Streaming) {
				Log.Debug("Returning, disabled or already streaming.");
				return Task.CompletedTask;
			}

			Streaming = true;
			
			return Task.CompletedTask;
		}

		public Task StopStream() {
			StopLights();
			Streaming = false;
			return Task.CompletedTask;
		}

		
		public Task FlashColor(Color color) {
			_strip?.SetAll(color);
			return Task.CompletedTask;
		}

		
		
		public void Dispose() {
			_strip?.Reset();
			_strip?.Dispose();
		}

		public Task ReloadData() {
			var ld = DataUtil.GetCollectionItem<LedData>("LedData", Id);
			if (ld == null) return Task.CompletedTask;
			Log.Debug("Reloading ledData: " + JsonConvert.SerializeObject(ld));
			Log.Debug("Old LED Data: " + JsonConvert.SerializeObject(Data));
			//con.LEDCount = _ledCount;
			Data = ld;
			_ledCount = Data.LedCount;
			_enableAbl = Data.AutoBrightnessLevel;
			if (_strip != null) {
				if (Data.Brightness != ld.Brightness) _strip.SetBrightness(ld.Brightness);
				if (Data.LedCount != ld.Count) _strip.SetLedCount(ld.Count);
				if (!_enableAbl) _strip.SetBrightness(ld.Brightness);
			}
			_offset = Data.Offset;
			Enable = Data.Enable;
			return Task.CompletedTask;
		}

	

		private Settings LoadLedData(LedData ld) {
			var settings = Settings.CreateDefaultSettings();
			if (!ld.FixGamma) settings.SetGammaCorrection(0, 0, 0);
			Log.Debug("Initializing LED Strip, type is " + ld.StripType);
			var stripType = ld.StripType switch {
				1 => StripType.SK6812W_STRIP,
				2 => StripType.WS2811_STRIP_RBG,
				0 => StripType.WS2812_STRIP,
				_ => StripType.WS2812_STRIP
			};
			
			// 18 = PWM0, 13 = PWM1, 21 = PCM, 10 = SPI0/MOSI
			var pin = ld.GpioNumber switch {
				19 => Pin.Gpio19,
				10 => Pin.Gpio10,
				18 => Pin.Gpio18,
				13 => Pin.Gpio13,
				_ => Pin.Gpio18
			};


			Log.Debug($@"Count, pin, type: {_ledCount}, {pin}, {(int) stripType}");
			
			settings.AddController(_ledCount, pin, stripType);
			Data = ld;
			return settings;
		}
		
		public void SetColor(List<Color> colors, List<Color> sectors, int fadeTime, bool force=false) {
			if (colors == null) {
				throw new ArgumentException("Invalid color input.");
			}

			if (Testing && !force || !Enable) {
				return;
			}

			var c1 = TruncateColors(colors, _ledCount, _offset);
			if (_enableAbl) {
				c1 = VoltAdjust(c1, Data);
			}

			for (var i = 0; i < c1.Count; i++) {
				var tCol = c1[i];
				if (Data.StripType == 1) {
					tCol = ColorUtil.ClampAlpha(tCol);
				}
				_strip?.SetLed(i,tCol);
			}
			_strip?.Render();
		}

		
		private static List<Color> TruncateColors(List<Color> input, int len, int offset) {
			var truncated = new List<Color>();
			// Subtract one from our offset because arrays
			// Start at the beginning
			if (offset + len > input.Count) {
				// Set the point where we need to end the loop
				var offsetLen = offset + len - input.Count;
				// Where do we start midway?
				var loopLen = input.Count - offsetLen;
				if (loopLen > 0) {
					for (var i = loopLen - 1; i < input.Count; i++) {
						truncated.Add(input[i]);
					}
				}

				// Now calculate how many are needed from the front
				for (var i = 0; i < len - offsetLen; i++) {
					truncated.Add(input[i]);
				}
			} else {
				for (var i = offset; i < offset + len; i++) {
					truncated.Add(input[i]);
				}    
			}

			return truncated;
		}

		private void StopLights() {
			Log.Debug("Stopping LED Strip.");
			_strip?.SetAll(Color.FromArgb(0,0,0,0));
			Log.Debug("LED Strips stopped.");
		}

		private List<Color> VoltAdjust(List<Color> input, LedData ld) {
			//power limit calculation
			//each LED can draw up 195075 "power units" (approx. 53mA)
			//one PU is the power it takes to have 1 channel 1 step brighter per brightness step
			//so A=2,R=255,G=0,B=0 would use 510 PU per LED (1mA is about 3700 PU)
			var actualMilliampsPerLed = ld.MilliampsPerLed; // 20
			var defaultBrightness = ld.Brightness;
			var ablMaxMilliamps = ld.AblMaxMilliamps; // 4500
			var length = input.Count;
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
					_strip?.SetBrightness((int)newBri);
					//Log.Debug($"Scaling brightness to {newBri / 255}.");
					CurrentMilliamps = powerSum0 * newBri / puPerMilliamp;
					if (newBri < defaultBrightness) {
						//output = ColorUtil.ClampBrightness(input, newBri);
					}
				} else {
					CurrentMilliamps = (float) powerSum / puPerMilliamp;
					if (defaultBrightness < 255) {
						_strip?.SetBrightness(defaultBrightness);
					}
				}

				CurrentMilliamps += length; //add standby power back to estimate
			} else {
				CurrentMilliamps = 0;
				if (defaultBrightness < 255) {
					_strip?.SetBrightness(defaultBrightness);
				}
			}

			return output;
		}

		private float scale8(float i, float scale) {
			return i * (scale / 256);
		}
	}
}