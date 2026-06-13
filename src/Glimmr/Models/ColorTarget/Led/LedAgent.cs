#region

using System;
using System.Drawing;
using Glimmr.Models.Data;
using Glimmr.Models.Util;
using Glimmr.Services;
using rpi_ws281x;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Led;

public class LedAgent : IColorTargetAgent {
	public bool Enable => _use0 || _use1;

	/// <summary>
	///     ABL scale recovery rate (per frame) when power headroom returns. Kept slow to avoid
	///     visible brightness "breathing".
	/// </summary>
	private const float AblReleaseRate = 0.01f;

	/// <summary>
	///     ABL scale attack blend (per frame) when over budget. Fast enough to protect the PSU,
	///     smooth enough to avoid flicker.
	/// </summary>
	private const float AblAttackBlend = 0.5f;

	/// <summary>
	///     Idle draw per LED (mA) - WS281x chips draw ~1mA even when dark.
	/// </summary>
	private const float IdleMilliampsPerLed = 1f;

	private float _ablAmps;
	private float _ablScale = 1f;
	private float _ablVolts;
	private Color[] _colors1;
	private Color[] _colors2;
	private Controller? _controller0;
	private Controller? _controller1;
	private LedData? _d0;
	private LedData? _d1;
	private bool _enableAbl;
	private float _gamma;
	private byte[] _gammaTable;
	private int _s0CurrentBrightness;
	private int _s0MaxBrightness;
	private int _s1CurrentBrightness;
	private int _s1MaxBrightness;
	private SystemData _sd;

	private bool _use0;
	private bool _use1;
	private WS281x? _ws281X;

	public LedAgent() {
		_sd = DataUtil.GetSystemData();
		_gamma = _sd.GammaCorrection;
		_gammaTable = ColorUtil.GammaTable(_gamma);
		_colors1 = Array.Empty<Color>();
		_colors2 = Array.Empty<Color>();
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
		_sd = DataUtil.GetSystemData();
		_gamma = _sd.GammaCorrection;
		_gammaTable = ColorUtil.GammaTable(_gamma);
		LedData? d0 = DataUtil.GetDevice<LedData>("0");
		LedData? d1 = DataUtil.GetDevice<LedData>("1");
		if (!SystemUtil.IsRaspberryPi()) {
			_d0 = d0;
			_d1 = d1;
			return;
		}

		var reloadStrips = _ws281X == null || StripConfigChanged(_d0, d0) || StripConfigChanged(_d1, d1);
		_d0 = d0;
		_d1 = d1;

		if (_d0 == null && _d1 == null) {
			_ws281X?.Dispose();
			_ws281X = null;
			_controller0 = null;
			_controller1 = null;
			_use0 = false;
			_use1 = false;
			return;
		}

		if (reloadStrips) {
			_ws281X?.Dispose();
			_ws281X = null;
			LoadStrips(_d0, _d1);
		}

		_use0 = _d0?.Enable == true && _controller0 != null;
		_use1 = _d1?.Enable == true && _controller1 != null;
		_enableAbl = _sd.EnableAutoBrightness;
		_ablVolts = _sd.AblVolts;
		_ablAmps = _sd.AblAmps;
		_ablScale = 1f;

		if (_use0 && _d0 != null) {
			_s0MaxBrightness = ToByteBrightness(_d0.Brightness);
			_s0CurrentBrightness = _s0MaxBrightness;
			_ws281X?.SetBrightness(_s0MaxBrightness);
		}

		if (!_use1 || _d1 == null) {
			return;
		}

		_s1MaxBrightness = ToByteBrightness(_d1.Brightness);
		_s1CurrentBrightness = _s1MaxBrightness;
		_ws281X?.SetBrightness(_s1MaxBrightness, 1);
	}

	/// <summary>
	///     Convert a device brightness value to the 0-255 scale used by the driver.
	///     Brightness is canonically a 0-100 percentage, but older configs stored 0-255;
	///     clamp those to full brightness rather than overflowing.
	/// </summary>
	private static int ToByteBrightness(int brightness) {
		var percent = Math.Clamp(brightness, 0, 100);
		return (int)(percent / 100f * 255f);
	}

	private static bool StripConfigChanged(LedData? current, LedData? next) {
		return current?.LedCount != next?.LedCount ||
		       current?.StripType != next?.StripType ||
		       current?.GpioNumber != next?.GpioNumber;
	}


	private void LoadStrips(LedData? d0, LedData? d1) {
		var settings = Settings.CreateDefaultSettings(false);
		var isPi5 = SystemUtil.IsRaspberryPi5();
		if (isPi5 && d1 is { Enable: true }) {
			// Pi 5 routes GPIO through the RP1 chip; the rp1_ws281x_pwm kernel module set up by the
			// Glimmr OS image only provides a single PWM output (GPIO 19). Dual strips are not
			// supported until a second RP1 PWM channel is validated.
			Log.Warning("Raspberry Pi 5 detected: second LED strip is not supported and will be disabled. " +
			            "Connect your strip data line to GPIO 19 and use LED device 0.");
			d1 = null;
		}

		if (d0 != null) {
			var pin0 = (Pin)d0.GpioNumber;
			if (isPi5 && d0.GpioNumber != 19) {
				Log.Warning("Raspberry Pi 5 detected: LED strip 0 is configured for GPIO {Gpio}, but Pi 5 " +
				            "WS281x output is only available on GPIO 19. Using GPIO 19.", d0.GpioNumber);
				pin0 = Pin.Gpio19;
			}

			_controller0 = settings.AddController(d0.LedCount, pin0, ToStripType(d0.StripType),
				ControllerType.PWM0, (byte)ToByteBrightness(d0.Brightness));
			_colors1 = ColorUtil.EmptyColors(d0.LedCount);
		} else {
			_controller0 = null;
			_colors1 = Array.Empty<Color>();
		}

		if (d1 != null) {
			_controller1 = settings.AddController(d1.LedCount, (Pin)d1.GpioNumber, ToStripType(d1.StripType),
				ControllerType.PWM1, (byte)ToByteBrightness(d1.Brightness));
			_colors2 = ColorUtil.EmptyColors(d1.LedCount);
		} else {
			_controller1 = null;
			_colors2 = Array.Empty<Color>();
		}

		if (_controller0 == null && _controller1 == null) {
			return;
		}

		try {
			_ws281X = new WS281x(settings);
		} catch (Exception e) {
			Log.Warning("Exception initializing WS281x driver: " + e.Message);
			_ws281X = null;
			_controller0 = null;
			_controller1 = null;
		}
	}

	private static StripType ToStripType(int stripType) {
		return stripType switch {
			1 => StripType.SK6812W_STRIP,
			2 => StripType.WS2811_STRIP_RBG,
			0 => StripType.WS2812_STRIP,
			_ => StripType.WS2812_STRIP
		};
	}

	public void SetColors(Color[] input) {
		var updated = false;
		if (_use0) {
			UpdateColors(input, "0");
			updated = true;
		}

		if (_use1) {
			UpdateColors(input, "1");
			updated = true;
		}

		if (updated) {
			Render();
		}
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


	/// <summary>
	///     Automatic brightness limiting: estimate the strips' current draw from the actual pixel
	///     values and scale global brightness down so the total stays inside the configured power
	///     budget (AblVolts * AblAmps). The scale is smoothed over time so limiting never flickers.
	/// </summary>
	private void VoltAdjust() {
		if (_ws281X == null || (!_use0 && !_use1)) {
			return;
		}

		// Total power available, with a 10% safety margin for the PSU.
		var budgetWatts = _ablVolts * _ablAmps * 0.9f;
		if (budgetWatts <= 0) {
			return;
		}

		var usageWatts = 0f;
		if (_use0 && _d0 != null) {
			usageWatts += EstimateWatts(_colors1, _d0, _s0MaxBrightness);
		}

		if (_use1 && _d1 != null) {
			usageWatts += EstimateWatts(_colors2, _d1, _s1MaxBrightness);
		}

		// Target scale: 1.0 when within budget, proportionally lower when over.
		var targetScale = usageWatts > budgetWatts ? budgetWatts / usageWatts : 1f;

		if (targetScale < _ablScale) {
			// Over budget: move down quickly (blend) to protect the supply.
			_ablScale += (targetScale - _ablScale) * AblAttackBlend;
		} else {
			// Headroom available: recover slowly to avoid visible pumping.
			_ablScale = Math.Min(targetScale, _ablScale + AblReleaseRate);
		}

		_ablScale = Math.Clamp(_ablScale, 0.05f, 1f);

		if (_use0) {
			var b0 = (int)(_s0MaxBrightness * _ablScale);
			if (b0 != _s0CurrentBrightness) {
				_ws281X.SetBrightness(b0);
				_s0CurrentBrightness = b0;
			}
		}

		if (!_use1) {
			return;
		}

		var b1 = (int)(_s1MaxBrightness * _ablScale);
		if (b1 == _s1CurrentBrightness) {
			return;
		}

		_ws281X.SetBrightness(b1, 1);
		_s1CurrentBrightness = b1;
	}

	/// <summary>
	///     Estimate strip power draw (watts) from per-LED color values.
	/// </summary>
	private float EstimateWatts(Color[] colors, LedData data, int maxBrightness) {
		var isRgbw = data.StripType == 1;
		var channels = isRgbw ? 4f : 3f;
		// Full-white per-channel draw in mA.
		var maPerChannel = data.MilliampsPerLed / channels;
		var totalMa = 0f;
		foreach (var t in colors) {
			var ledMa = (t.R + t.G + t.B) / 255f * maPerChannel;
			if (isRgbw) {
				ledMa += t.A / 255f * maPerChannel;
			}

			totalMa += Math.Max(IdleMilliampsPerLed, ledMa);
		}

		// PWM brightness scales output (and therefore draw) linearly.
		totalMa *= maxBrightness / 255f * _ablScale;
		return totalMa / 1000f * _ablVolts;
	}


	private void UpdateColors(Color[] colors, string id) {
		var data = id == "0" ? _d0 : _d1;
		if (data == null) {
			return;
		}

		var target = id == "0" ? _colors1 : _colors2;
		if (target.Length != data.LedCount) {
			target = ColorUtil.EmptyColors(data.LedCount);
			if (id == "0") {
				_colors1 = target;
			} else {
				_colors2 = target;
			}
		}

		if (colors.Length == 0) {
			return;
		}

		// Inline truncate/offset/multiplier mapping into the persistent buffer - no allocations.
		var len = data.LedCount;
		var offset = (data.Offset % colors.Length + colors.Length) % colors.Length;
		var multiplier = data.LedMultiplier;
		var reverse = data.ReverseStrip;
		var applyGamma = data.FixGamma && _gamma > 1f;
		var gamma = _gammaTable;
		for (var i = 0; i < len; i++) {
			var tgt = (int)((offset + i * multiplier) % colors.Length);
			var c = colors[tgt];
			if (applyGamma) {
				c = Color.FromArgb(c.A, gamma[c.R], gamma[c.G], gamma[c.B]);
			}

			target[reverse ? len - 1 - i : i] = c;
		}
	}

	public void SetColor(Color color, string id) {
		// Fill our persistent buffers and push via SetLEDS rather than Controller.SetAll,
		// which was a no-op in older driver versions.
		if (_use0 && id == "0") {
			for (var i = 0; i < _colors1.Length; i++) {
				_colors1[i] = color;
			}

			_controller0?.SetLEDS(_colors1);
		}

		if (_use1 && id == "1") {
			for (var i = 0; i < _colors2.Length; i++) {
				_colors2[i] = color;
			}

			_controller1?.SetLEDS(_colors2);
		}

		_ws281X?.Render();
	}

	public void Clear() {
		if (Enable) {
			_ws281X?.Reset();
		}
	}
}
