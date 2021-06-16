using Glimmr.Models.Util;
using Glimmr.Services;
using rpi_ws281x;

namespace Glimmr.Models.ColorTarget.Led {
	public class LedAgent : IColorTargetAgent {
		public WS281x Ws281x;
		
		public void Dispose() {
			Ws281x.Dispose();
		}

		public dynamic CreateAgent(ControlService cs) {
			LedData d0 = DataUtil.GetDevice<LedData>("0");
			LedData d1 = DataUtil.GetDevice<LedData>("1");
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

			var c0 = settings.AddController(ControllerType.PWM0, d0.LedCount, stripType0,(byte) d0.Brightness);
			var c1 = settings.AddController(ControllerType.PWM1, d1.LedCount, stripType1,(byte) d1.Brightness);
			Ws281x = new WS281x(settings);
			return Ws281x;
		}
	}
}