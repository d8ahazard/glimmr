using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Glimmr.Models.Util;
using rpi_ws281x;
using Serilog;

namespace Glimmr.Models.LED {
	public sealed class LedStrip : IDisposable {
		private int _ledCount;
		private WS281x _strip;
		private Controller _controller;
		private LedData _ld;
		private bool _testing;
		public float CurrentMilliamps { get; set; }
        
		public LedStrip(LedData ld) {
			Initialize(ld);
		}

		public void Reload(LedData ld) {
			Log.Debug("Setting brightness to " + ld.Brightness);
			_controller.Brightness = (byte) ld.Brightness;
			if (_ledCount != ld.LedCount) {
				_strip?.Dispose();
				Initialize(ld);
			}
		}

		private void Initialize(LedData ld) {
			_ld = ld ?? throw new ArgumentException("Invalid LED Data.");
			Log.Debug("Initializing LED Strip, type is " + ld.StripType);
			_ledCount = ld.LeftCount + ld.RightCount + ld.TopCount + ld.BottomCount;
			var stripType = ld.StripType switch {
				1 => StripType.SK6812W_STRIP,
				2 => StripType.WS2811_STRIP_RBG,
				0 => StripType.WS2812_STRIP,
				_ => StripType.SK6812W_STRIP
			};
			var pin = Pin.Gpio18;
			if (ld.PinNumber == 13) pin = Pin.Gpio13;
			Log.Debug($@"Count, pin, type: {_ledCount}, {ld.PinNumber}, {(int)stripType}");
			var settings = Settings.CreateDefaultSettings();
			_controller = settings.AddController(_ledCount, pin, stripType);
			try {
				_strip = new WS281x(settings);
				Log.Debug($@"Strip created using {_ledCount} LEDs.");
                
			} catch (DllNotFoundException) {
				Log.Debug("Unable to initialize strips, we're not running on a pi!");
			}
		}

        
		public void StartTest(int len, int test) {
			_testing = true;
			var lc = len;
			if (len < _ledCount) {
				lc = _ledCount;
			}
			var colors = new Color[lc];
			colors = ColorUtil.EmptyColors(colors);

			if (test == 0) {
				var c1 = _ld.LeftCount - 1;
				var c2 = _ld.LeftCount + _ld.TopCount - 1;
				var c3 = _ld.LeftCount + _ld.TopCount + _ld.LeftCount - 1;
				var c4 = _ld.LeftCount * 2 + _ld.TopCount * 2 - 1;
				colors[c1] = Color.FromArgb(255, 0, 0, 255);
				if (c2 <= len) colors[c2] = Color.FromArgb(255, 255, 0, 0);
				if (c3 <= len) colors[c3] = Color.FromArgb(255, 0, 255, 0);
				if (c4 <= len) colors[c4] = Color.FromArgb(255, 0, 255, 255);
				colors[len - 1] = Color.FromArgb(255, 255, 255, 255);
				Log.Debug($"Corners at: {c1}, {c2}, {c3}, {c4}");
			} else {
				colors[len] = Color.FromArgb(255, 255, 0, 0);
			}

			UpdateAll(colors.ToList(), true);
		}

        
		public void StopTest() {
			_testing = false;
			var mt = ColorUtil.EmptyColors(new Color[_ld.LedCount]);
			UpdateAll(mt.ToList(), true);
		}
        
		public void UpdateAll(List<Color> colors, bool force=false) {
			//Log.Debug("NOT UPDATING.");
			if (colors == null) throw new ArgumentException("Invalid color input.");
			if (_testing && !force) return;
			
			// Thanks, WLED!
			if (_ld.AutoBrightnessLevel) VoltAdjust(colors);

			var iSource = 0;
			for (var i = 0; i < _ledCount; i++) {
				if (iSource >= colors.Count) {
					iSource = 0; // reset if at end of source
				}

				var tCol = colors[iSource];
				if (_ld.FixGamma)  {
					//tCol = ColorUtil.FixGamma2(tCol);
				}

				if (_ld.StripType == 1) {
					tCol = ColorUtil.ClampAlpha(tCol);    
				}

				_controller.SetLED(i, tCol);
				iSource++;
			}
            
			_strip?.Render();
		}

		public void StopLights() {
			Log.Debug("Stopping LED Strip.");
			for (var i = 0; i < _ledCount; i++) {
				_controller.SetLED(i, Color.FromArgb(0, 0, 0, 0));
			}
			_strip?.Render();
			Log.Debug("LED Strips stopped.");
		}

        

		public void Dispose() {
			_strip?.Dispose();
		}
        
		private void VoltAdjust(List<Color> input) {
			//power limit calculation
			//each LED can draw up 195075 "power units" (approx. 53mA)
			//one PU is the power it takes to have 1 channel 1 step brighter per brightness step
			//so A=2,R=255,G=0,B=0 would use 510 PU per LED (1mA is about 3700 PU)
			var actualMilliampsPerLed = _ld.MilliampsPerLed;
			var ablMaxMilliamps = _ld.AblMaxMilliamps;
			var length = input.Count;
			
			if (ablMaxMilliamps > 149 && actualMilliampsPerLed > 0) { //0 mA per LED and too low numbers turn off calculation
  
				var puPerMilliamp = 195075 / actualMilliampsPerLed;
				var powerBudget = ablMaxMilliamps * puPerMilliamp; //100mA for ESP power
				if (powerBudget > puPerMilliamp * length) { //each LED uses about 1mA in standby, exclude that from power budget
					powerBudget -= puPerMilliamp * length;
				} else {
					powerBudget = 0;
				}

				var powerSum = 0;

				for (var i = 0; i < length; i++) { //sum up the usage of each LED
					var c = input[i];
					powerSum += c.R + c.G + c.B + c.A;
				}

				if (_ld.StripType == 1) { //RGBW led total output with white LEDs enabled is still 50mA, so each channel uses less
					powerSum *= 3;
					powerSum >>= 2; //same as /= 4
				}

				var powerSum0 = powerSum;
				powerSum *= _ld.Brightness;
    
				if (powerSum > powerBudget) { //scale brightness down to stay in current limit
					var scale = powerBudget / (float)powerSum;
					var scaleI = scale * 255;
					var scaleB = scaleI > 255 ? 255 : scaleI;
					var newBri = scale8(_ld.Brightness, scaleB);
					_controller.Brightness = (byte) newBri;
					CurrentMilliamps = powerSum0 * newBri / puPerMilliamp;
				} else {
					CurrentMilliamps = (float) powerSum / puPerMilliamp;
					_controller.Brightness = (byte) _ld.Brightness;
				}
				CurrentMilliamps += length; //add standby power back to estimate
			} else {
				CurrentMilliamps = 0;
				_controller.Brightness = (byte) _ld.Brightness;
			}
  
		}

		private float scale8(float i, float scale) {
			return i * (scale / 256);
		}
	}
}