using System;
using System.Collections.Generic;
using System.Drawing;
using HueDream.Models.Util;
using rpi_ws281x;

namespace HueDream.Models.LED {
    public sealed class LedStrip : IDisposable {
        private readonly int _ledCount;
        private readonly WS281x _strip;
        
        public LedStrip(LedData ld) {
            if (ld == null) throw new ArgumentException("Invalid LED Data.");
            LogUtil.Write("Initializing LED Strip, type is " + ld.StripType);
            _ledCount = ld.VCount * 2 + ld.HCount * 2;
            var stripType = ld.StripType switch {
                1 => StripType.SK6812_STRIP_GRBW,
                2 => StripType.WS2811_STRIP_RBG,
                0 => StripType.WS2812_STRIP,
                _ => StripType.SK6812_STRIP_GRBW
            };
            LogUtil.Write($@"Count, pin, type: {_ledCount}, {ld.PinNumber}, {(int)stripType}");
            var settings = Settings.CreateDefaultSettings();
            settings.Channel_1 = new Channel(_ledCount, ld.PinNumber, (byte) ld.Brightness, false, stripType);
            _strip = new WS281x(settings);
            LogUtil.Write($@"Strip created using {_ledCount} LEDs.");
            Demo();
        }

        private void Demo() {
            for (var i = 0; i < _ledCount; i++) {
                var pi = i * 1.0f;
                var progress = pi / _ledCount;
                var rCol = Rainbow(progress);
                _strip.SetLEDColor(0, i, rCol);
                _strip.Render();
            }

            System.Threading.Thread.Sleep(500);

            StopLights();
        }
        
        public void UpdateAll(List<Color> colors) {
            if (colors == null) throw new ArgumentException("Invalid color input.");
            var iSource = 0;
            for (var i = 0; i < _ledCount; i++) {
                if (iSource >= colors.Count) {
                    iSource = 0; // reset if at end of source
                }

                var tCol = colors[iSource];
                var aAverage = tCol.R + tCol.B + tCol.G;
                var alpha = 0;
                if (aAverage > 750) alpha = 255;
                var col = Color.FromArgb(alpha, tCol.R, tCol.G, tCol.B);
                _strip.SetLEDColor(0, i, col);
                iSource++;
            }
            _strip.Render();
        }

        public void StopLights() {
            LogUtil.Write("Stopping LED Strip.");
            for (var i = 0; i < _ledCount; i++) {
                _strip.SetLEDColor(0, i, Color.FromArgb(0, 0, 0, 0));
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