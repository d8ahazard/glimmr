using System;
using System.Collections.Generic;
using System.Drawing;
using HueDream.Models.StreamingDevice.WLed;
using HueDream.Models.Util;
using rpi_ws281x;
using ColorUtil = HueDream.Models.Util.ColorUtil;

namespace HueDream.Models.LED {
    public sealed class LedStrip : IDisposable {
        private readonly int _ledCount;
        private readonly WS281x _strip;
        private readonly Controller _controller;
        private LedData _ld;
        
        public LedStrip(LedData ld) {
            _ld = ld ?? throw new ArgumentException("Invalid LED Data.");
            LogUtil.Write("Initializing LED Strip, type is " + ld.StripType);
            _ledCount = ld.VCount * 2 + ld.HCount * 2;
            var stripType = ld.StripType switch {
                1 => StripType.SK6812W_STRIP,
                2 => StripType.WS2811_STRIP_RBG,
                0 => StripType.WS2812_STRIP,
                _ => StripType.SK6812W_STRIP
            };
            var pin = Pin.Gpio18;
            if (ld.PinNumber == 13) pin = Pin.Gpio13;
            LogUtil.Write($@"Count, pin, type: {_ledCount}, {ld.PinNumber}, {(int)stripType}");
            var settings = Settings.CreateDefaultSettings();
            _controller = settings.AddController(_ledCount, pin, stripType, ControllerType.PWM0, (byte)ld.Brightness);
            _strip = new WS281x(settings);
            LogUtil.Write($@"Strip created using {_ledCount} LEDs.");
            Demo();
        }

        private void Demo() {
            var wlData = DataUtil.GetCollection<WLedData>("wled");
            var strips = new List<WLedStrip>();
            foreach (var wl in wlData) {
                strips.Add(new WLedStrip(wl));
            }

            foreach (var s in strips) {
                s.StartStream();
            }
            for (var i = 0; i < _ledCount; i++) {
                var pi = i * 1.0f;
                var progress = pi / _ledCount;
                var rCol = Rainbow(progress);
                _controller.SetLED(i, rCol);
                _strip.Render();
                foreach (var s in strips) {
                    // Total index of LEDs
                    var lastIndex = s.Data.LedCount + s.Data.Offset;
                    // Value we should start at if the strip loops
                    var reStartIndex = -1;
                    var reStartStop = 0;
                    // If we have more LEDs in index than count, start from zero
                    if (lastIndex > _ledCount) {
                        reStartIndex = 0;
                        reStartStop = lastIndex - _ledCount;
                    } 
                    // If i is between offset or end of all colors, set that pixel
                    if (s.Data.Offset >= i && i <= s.Data.LedCount) {
                        s.UpdatePixel(i,rCol);
                    } else if (reStartIndex <= i && i < reStartStop ) {
                        s.UpdatePixel(i,rCol);
                    }
                }
            }
            foreach (var s in strips) {
                s.StopStream();
            }

            System.Threading.Thread.Sleep(500);
            StopLights();
        }
        
        public void UpdateAll(List<Color> colors) {
            //LogUtil.Write("NOT UPDATING.");
            if (colors == null) throw new ArgumentException("Invalid color input.");
            var iSource = 0;
            for (var i = 0; i < _ledCount; i++) {
                if (iSource >= colors.Count) {
                    iSource = 0; // reset if at end of source
                }

                var tCol = colors[iSource];
                if (!_ld.FixGamma)  {
                    tCol = ColorUtil.FixGamma2(tCol);
                }

                if (_ld.StripType == 1) {
                    tCol = ColorUtil.ClampAlpha(tCol);    
                }

                _controller.SetLED(i, tCol);
                iSource++;
            }
            _strip.Render();
        }

        public void StopLights() {
            LogUtil.Write("Stopping LED Strip.");
            for (var i = 0; i < _ledCount; i++) {
                _controller.SetLED(i, Color.FromArgb(0, 0, 0, 0));
            }
            _strip.Render();
            LogUtil.Write("LED Strips stopped.");
        }

        private static Color Rainbow(float progress) {
            var div = Math.Abs(progress % 1) * 6;
            var ascending = (int) (div % 1 * 255);
            var descending = 255 - ascending;
            var alpha = 0;
            return (int) div switch {
                0 => Color.FromArgb(alpha, 255, ascending, 0),
                1 => Color.FromArgb(alpha, descending, 255, 0),
                2 => Color.FromArgb(alpha, 0, 255, ascending),
                3 => Color.FromArgb(alpha, 0, descending, 255),
                4 => Color.FromArgb(alpha, ascending, 0, 255),
                _ => Color.FromArgb(alpha, 255, 0, descending)
            };
        }

        public void Dispose() {
            _strip?.Dispose();
        }
    }
}