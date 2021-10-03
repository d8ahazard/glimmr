#region

using System;
using System.Drawing;
using System.Linq;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using rpi_ws281x;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Led {
	public class LedAgent : IColorTargetAgent {
		private WS281x? _ws281X;
		private float _ablAmps;
		private float _ablVolts;
		private Color[] _colors1;
		private Color[] _colors2;
		private Controller? _controller0;
		private Controller? _controller1;
		private LedData? _d0;
		private LedData? _d1;
		private bool _enableAbl;
		private SystemData _sd;

		private bool _use0;
		private bool _use1;
		private int _s0Brightness;
		private int _s1Brightness;
		private int _s0MaxBrightness;
		private int _s1MaxBrightness;

		public LedAgent() {
			_colors1 = ColorUtil.EmptyColors(_d0?.LedCount ?? 0);
			_colors2 = ColorUtil.EmptyColors(_d1?.LedCount ?? 0);
			_sd = DataUtil.GetSystemData();
			_ablAmps = _sd.AblAmps;
			_ablVolts = _sd.AblVolts;
			ReloadData();
		}

		public void Dispose() {
			GC.SuppressFinalize(this);
			_ws281X?.Dispose();
		}

		public dynamic? CreateAgent(ControlService cs) {
			if (!SystemUtil.IsRaspberryPi()) {
				return null;
			}

			cs.RefreshSystemEvent += ReloadData;
			return this;
		}

		public void ReloadData() {
			Log.Debug("Reloading...");
			_sd = DataUtil.GetSystemData();
			LedData? d0 = DataUtil.GetDevice<LedData>("0");
			LedData? d1 = DataUtil.GetDevice<LedData>("1");
			if (!SystemUtil.IsRaspberryPi() || d0 == null || d1 == null || _d0 == null || _d1 == null) {
				_d0 = d0;
				_d1 = d1;
				return;
			}

			if (d0.StripType != _d0.StripType || d1.StripType != _d1.StripType) {
				_ws281X?.Dispose();
				LoadStrips(d0, d1);
			}
			Log.Debug("Reloading led data for real...");
			_d0 = d0;
			_d1 = d1;
			
			if (_ws281X == null) LoadStrips(_d0, _d1);
			_use0 = _d0.Enable;
			_use1 = _d1.Enable;
			_enableAbl = _sd.EnableAutoBrightness;
			_ablVolts = _sd.AblVolts;
			_ablAmps = _sd.AblAmps;
			
			if (_use0) {
				_s0Brightness = (int) (_d0.Brightness / 100f * 255f);
				_s0MaxBrightness = _s0Brightness;
				Log.Debug("Setting brightness to " + _s0Brightness);
				_ws281X?.SetBrightness(_s0Brightness);
			}

			if (_use1) {
				_s1Brightness = (int) (_d1.Brightness / 100f * 255f);
				_s1MaxBrightness = _s1Brightness;
				_ws281X?.SetBrightness(_s1Brightness, 1);
			}
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

			_controller0 = settings.AddController(ControllerType.PWM0, d0.LedCount, stripType0, (byte)d0.Brightness);
			_controller1 = settings.AddController(ControllerType.PWM1, d1.LedCount, stripType1, (byte)d1.Brightness);
			_colors1 = ColorUtil.EmptyColors(d0.LedCount);
			_colors2 = ColorUtil.EmptyColors(d1.LedCount);
			_ws281X = new WS281x(settings);
		}

		public void SetColors(Color[] input) {
			if (_d0?.Enable ?? false) {
				SetColors(input, "0");
			}

			if (_d1?.Enable ?? false) {
				SetColors(input, "1");
			}

			Render();
		}

		private void Render() {
			if (_enableAbl) {
				VoltAdjust();
			}
			if (_use0) {
				_controller0?.SetLEDS(_colors1);
			}

			if (_use1) {
				_controller1?.SetLEDS(_colors2);
			}

			if (_use0 || _use1) {
				_ws281X?.Render();
			}
		}


		private void VoltAdjust() {
			// Gonna do this from scratch. According to ws2812B docs, modern B strips should
			// use .3w/LED, or 3000 milliamps.
			if (_d0 == null || _d1 == null) {
				return;
			}

			var strip0Draw = _d0.MilliampsPerLed; // 20
			var strip1Draw = _d1.MilliampsPerLed;
			
			// Total power we have at our disposal
			var totalWatts = _ablVolts * _ablAmps;
			// Subtract CPU usage (Probably needs more for splitter, etc)
			// This should totally work
			var totalCost = 0;
			if (_d0.Enable) {
				totalCost += _d0.MilliampsPerLed * _d0.LedCount;
			}

			if (_d1.Enable) {
				totalCost += _d1.MilliampsPerLed * _d1.LedCount;
			}

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

					use *= _s0MaxBrightness / 255f;
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

					use *= _s1MaxBrightness / 255f;
					usage += use;
				}
			}

			if (usage > totalWatts) {
				//scale brightness down to stay in current limit
				var scale = totalWatts / usage;
				
				if (_use0) {
					var scaleI = scale * _s0MaxBrightness;
					_s0Brightness = LerpBrightness(_s0Brightness, scaleI, _s0MaxBrightness);
					_ws281X?.SetBrightness(_s0Brightness);
				}

				if (_use1) {
					var scaleI = scale * _s1MaxBrightness;
					_s1Brightness = LerpBrightness(_s1Brightness, scaleI, _s1MaxBrightness);
					_ws281X?.SetBrightness(_s1Brightness);
				}
			} else {
				if (_use0 && _s0Brightness < _s0MaxBrightness) {
					_ws281X?.SetBrightness(LerpBrightness(_s0Brightness, _s0MaxBrightness, _s0MaxBrightness));
				}

				if (_use1 && _s1Brightness < _s0MaxBrightness) {
					_ws281X?.SetBrightness(LerpBrightness(_s1Brightness, _s1MaxBrightness, _s1MaxBrightness), 1);
				}
			}
		}

		private int LerpBrightness(int current, float target, int max) {
			int output = (int) Math.Min(target, max);
			if (output > current) {
				output = Math.Min(current + 1, max);
			} else {
				output = (int)target;
			}
			var op = output;
			return op;
		}


		private void SetColors(Color[] colors, string id) {
			if (_d0 == null || _d1 == null) {
				return;
			}

			var data = id == "0" ? _d0 : _d1;
			var toSend = ColorUtil.TruncateColors(colors, data.Offset, data.LedCount, data.LedMultiplier);
			if (data.ReverseStrip) {
				toSend = toSend.Reverse().ToArray();
			}

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
			if (_use0 && id == "0") {
				_controller0?.SetAll(color);
			}

			if (_use1 && id == "1") {
				_controller1?.SetAll(color);
			}

			_ws281X?.Render();
		}

		public void Clear() {
			if (_use0) {
				_controller0?.SetAll(Color.Empty);
			}

			if (_use1) {
				_controller1?.SetAll(Color.Empty);
			}

			_ws281X?.Render();
		}
	}
}