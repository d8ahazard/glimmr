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
                
            } catch (DllNotFoundException) {
                LogUtil.Write("Unable to initialize strips, we're not running on a pi!");
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
                LogUtil.Write($"Corners at: {c1}, {c2}, {c3}, {c4}");
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

        

        public void Dispose() {
            _strip?.Dispose();
        }
    }
}