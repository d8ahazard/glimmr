#region

using System;
using System.Drawing;
using System.Linq;
using Glimmr.Models.Util;
using Glimmr.Services;
using rpi_ws281x;

#endregion

namespace Glimmr.Models.ColorTarget.Led {
	public class LedAgent : IColorTargetAgent {
		public WS281x? Ws281X { get; private set; }
		private Controller? _controller0;
		private Controller? _controller1;

		private bool _use0;
		private bool _use1;
		private Color[] _colors1;
		private Color[] _colors2;
		private LedData? _d0;
		private LedData? _d1;
		private SystemData _sd;
		private bool _enableAbl;
		private float _ablVolts;
		private float _ablAmps;

		public void Dispose() {
			GC.SuppressFinalize(this);
			Ws281X?.Dispose();
		}

		public LedAgent() {
			_colors1 = ColorUtil.EmptyColors(_d0?.LedCount ?? 0);
			_colors2 = ColorUtil.EmptyColors(_d1?.LedCount ?? 0);
			_sd = DataUtil.GetSystemData();
			_ablAmps = _sd.AblAmps;
			_ablVolts = _sd.AblVolts;
		}

		public dynamic? CreateAgent(ControlService cs) {
			_d0 = new LedData();
			_d1 = new LedData();
			if (!SystemUtil.IsRaspberryPi()) {
				return null;
			}

			cs.RefreshSystemEvent += ReloadData;

			_d0 = DataUtil.GetDevice<LedData>("0");
			_d1 = DataUtil.GetDevice<LedData>("1");
			if (_d0 == null || _d1 == null) {
				return null;
			}

			LoadStrips(_d0, _d1);
			
			ReloadData();
			return this;
		}

		public void ReloadData() {
			_sd = DataUtil.GetSystemData();
			LedData? d0 = DataUtil.GetDevice<LedData>("0");
			LedData? d1 = DataUtil.GetDevice<LedData>("1");
			if (!SystemUtil.IsRaspberryPi() || d0 == null || d1 == null || _d0 == null || _d1 == null) return;
			if (d0.StripType != _d0.StripType || d1.StripType != _d1.StripType) {
				Ws281X?.Dispose();
				LoadStrips(d0, d1);
			}
			_d0 = d0;
			_d1 = d1;
			_use0 = _d0.Enable;
			_use1 = _d1.Enable;
			_enableAbl = _sd.EnableAutoBrightness;
			_ablVolts = _sd.AblVolts;
			_ablAmps = _sd.AblAmps;
			if (_sd.EnableAutoBrightness) {
				return;
			}

			if (_use0) Ws281X?.SetBrightness((int) (_d0.Brightness / 100f * 255f));
			if (_use0) Ws281X?.SetBrightness((int) (_d1.Brightness / 100f * 255f));
		}
		

		private void LoadStrips(LedData d0, LedData d1) {
			var settings = Settings.CreateDefaultSettings();
			var stripType0 = d0.StripType switch {
				1 => StripType.SK6812W_STRIP,
				2 => StripType.WS2811_STRIP_RBG,
				0 => StripType.WS2812_STRIP,
				_ => StripType.WS2812_STRIP
			};

			var stripType1 = d1.StripType switch {
				1 => StripType.SK6812W_STRIP,
				2 => StripType.WS2811_STRIP_RBG,
				0 => StripType.WS2812_STRIP,
				_ => StripType.WS2812_STRIP
			};

			_controller0 = settings.AddController(ControllerType.PWM0, d0.LedCount, stripType0, (byte) d0.Brightness);
			_controller1 = settings.AddController(ControllerType.PWM1, d1.LedCount, stripType1, (byte) d1.Brightness);
			_colors1 = ColorUtil.EmptyColors(d0.LedCount);
			_colors2 = ColorUtil.EmptyColors(d1.LedCount);
			Ws281X = new WS281x(settings);
		}

		public void SetColors(Color[] input) {
			if (_d0?.Enable ?? false) SetColors(input, "0");
			if (_d1?.Enable ?? false) SetColors(input, "1");
			Render();
		}

		private void Render() {
			if (_enableAbl) {
				VoltAdjust();
			}
			if (_use0) _controller0?.SetLEDS(_colors1);
			if (_use1) _controller1?.SetLEDS(_colors2);
			if (_use0 || _use1) Ws281X?.Render();
		}
		
		
		private void VoltAdjust() {
			// Gonna do this from scratch. According to ws2812B docs, modern B strips should
			// use .3w/LED, or 3000 milliamps.
			if (_d0 == null || _d1 == null) return;
			var strip0Draw = _d0.MilliampsPerLed; // 20
			var strip1Draw = _d1.MilliampsPerLed;
			var strip0Brightness = (int) (_d0.Brightness / 100f * 255f) ;
			var strip1Brightness = (int) (_d1.Brightness / 100f * 255f) ;
			
			// Total power we have at our disposal
			var totalWatts = _ablVolts * _ablAmps;
			// Subtract CPU usage (Probably needs more for splitter, etc)
			totalWatts -= 6.5f;
			// This should totally work
			var totalCost = 0;
			if (_d0.Enable) totalCost += _d0.MilliampsPerLed * _d0.LedCount;
			if (_d1.Enable) totalCost += _d1.MilliampsPerLed * _d1.LedCount;
			var usage = 0f;
			// Loop each LED, subtract it's current score 
			if (totalWatts <= totalCost) {
				if (_d0.Enable) {
					var use = 0f;
					if (_d0.StripType == 1) {
						var l1 = strip0Draw / 4f;
						foreach (var t in _colors1) {
							use = t.R / 255f * l1 + t.G / 255f * l1 + t.B / 255f * l1 + t.A / 255f * l1;
							// Each LED uses a minimum of .1w even when off.
							use = Math.Max(.1f, use);
						}
					} else {
						var l1 = strip0Draw / 3f;
						foreach (var t in _colors1) {
							use = t.R / 255f * l1 + t.G / 255f * l1 + t.B / 255f * l1;
							use = Math.Max(.1f, use);
						}
					}

					use *= strip0Brightness / 255f;
					usage += use;
				}

				if (_d1.Enable) {
					var use = 0f;
					if (_d1.StripType == 1) {
						var l1 = strip1Draw / 4f;
						foreach (var t in _colors2) {
							use = t.R / 255f * l1 + t.G / 255f * l1 + t.B / 255f * l1 + t.A / 255f * l1;
							use = Math.Max(10f, use);
						}
					} else {
						var l1 = strip1Draw / 3f;
						foreach (var t in _colors2) {
							use = t.R / 255f * l1 + t.G / 255f * l1 + t.B / 255f * l1;
							use = Math.Max(10f, use);
						}
					}
					use *= strip0Brightness / 255f;
					usage += use;
				}
			}

			if (usage > totalWatts) {
				//scale brightness down to stay in current limit
				var scale = totalWatts / usage;
				var scaleI = scale * 255;
				if (_use0) Ws281X?.SetBrightness((int) Math.Min(strip0Brightness, scaleI));
				if (_use1) Ws281X?.SetBrightness((int) Math.Min(strip1Brightness, scaleI), 1);
			} else {
				if (_use0 && strip0Brightness < 255) {
					Ws281X?.SetBrightness(strip0Brightness);
				}
				if (_use1 && strip1Brightness < 255) {
					Ws281X?.SetBrightness(strip0Brightness, 1);
				}
			}
		}
		
		private void SetColors(Color[] colors, string id) {
			if (_d0 == null || _d1 == null) return;
			var data = id == "0" ? _d0 : _d1;
			var toSend = ColorUtil.TruncateColors(colors, data.Offset, data.LedCount, data.LedMultiplier);
			if (data.ReverseStrip) toSend = toSend.Reverse().ToArray();
			if (data.StripType == 1) {
				for (var i = 0; i < toSend.Length; i++) {
					var tCol = toSend[i];
					tCol = ColorUtil.ClampAlpha(tCol);
					toSend[i] = tCol;
				}
			}

			if (id == "0") {
				_colors1 = toSend;
			} else {
				_colors2 = toSend;
			}
		}

		public void SetColor(Color color, string id) {
			if (_use0 && id == "0") _controller0?.SetAll(color);
			if (_use1 && id == "1") _controller1?.SetAll(color);
			Ws281X?.Render();
		}

		public void Clear() {
			if (_use0) _controller0?.SetAll(Color.Empty);
			if (_use1) _controller1?.SetAll(Color.Empty);
			Ws281X?.Render();
		}
	}
}