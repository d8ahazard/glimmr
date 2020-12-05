using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.StreamingDevice.WLed;
using Glimmr.Models.Util;
using ManagedBass.DirectX8;
using Q42.HueApi.Models.Gamut;
using rpi_ws281x;
using SimpleHttpServer.Helper;
using ColorUtil = Glimmr.Models.Util.ColorUtil;

namespace Glimmr.Models.LED {
    public sealed class LedStrip : IDisposable {
        private int _ledCount;
        private WS281x _strip;
        private Controller _controller;
        private LedData _ld;
        private bool _testing = false;
        
        public LedStrip(LedData ld) {
            Initialize(ld, true);
        }

        public void Reload(LedData ld) {
            LogUtil.Write("Setting brightness to " + ld.Brightness);
            _controller.Brightness = (byte) ld.Brightness;
            if (_ledCount != ld.LedCount) {
                _strip?.Dispose();
                Initialize(ld);
            }
        }

        private void Initialize(LedData ld, bool demo = false) {
            _ld = ld ?? throw new ArgumentException("Invalid LED Data.");
            LogUtil.Write("Initializing LED Strip, type is " + ld.StripType);
            _ledCount = ld.LeftCount + ld.RightCount + ld.TopCount + ld.BottomCount;
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
            _controller = settings.AddController(_ledCount, pin, stripType, ControllerType.PWM0, (byte)255);
            try {
                _strip = new WS281x(settings);
                LogUtil.Write($@"Strip created using {_ledCount} LEDs.");
                if (demo) Demo();
            } catch (DllNotFoundException) {
                LogUtil.Write("Unable to initialize strips, we're not running on a pi!");
            }
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

        public void StartTest(int len, int test) {
            _testing = true;
            var lc = len;
            if (len < _ledCount) {
                lc = _ledCount;
            }
            var colors = new Color[lc];
            colors = EmptyColors(colors);

            if (test == 0) {
                var counter = 0;
                var c0 = 0;
                var c1 = _ld.LeftCount - 1;
                var c2 = _ld.LeftCount + _ld.TopCount - 1;
                var c3 = _ld.LeftCount + _ld.TopCount + _ld.LeftCount - 1;
                var c4 = _ld.LeftCount * 2 + _ld.TopCount * 2 - 1;
                colors[c1] = Color.FromArgb(255, 0, 0, 255);
                if (c2 <= len) colors[c2] = Color.FromArgb(255, 255, 0, 0);
                if (c3 <= len) colors[c3] = Color.FromArgb(255, 0, 255, 0);
                if (c4 <= len) colors[c4] = Color.FromArgb(255, 0, 255, 255);
                colors[len - 1] = Color.FromArgb(255, 255, 255, 255);
                LogUtil.Write($"Corners at: {c1}, {c2}, {c3}, {c4}");
            } else {
                colors[len] = Color.FromArgb(255, 255, 0, 0);
            }

            UpdateAll(colors.ToList(), true);
        }

        private Color[] EmptyColors(Color[] input) {
            for (var i = 0; i < input.Length; i++) {
                input[i] = Color.FromArgb(0, 0, 0, 0);
            }

            return input;
        }

        public void StopTest() {
            _testing = false;
            var mt = EmptyColors(new Color[_ld.LedCount]);
            UpdateAll(mt.ToList(), true);
        }
        
        public void UpdateAll(List<Color> colors, bool force=false) {
            //LogUtil.Write("NOT UPDATING.");
            if (colors == null) throw new ArgumentException("Invalid color input.");
            if (_testing && !force) return;
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