using System;
using System.Collections.Generic;
using System.Drawing;
using HueDream.Models.Util;
using ws281x.Net;

namespace HueDream.Models.LED {
    public sealed class LedStrip : IDisposable {
        public int Brightness { get; set; }
        private int StartupAnimation { get; }

        private readonly int ledCount;

        private readonly Neopixel neoPixel;
        
        public LedStrip(LedData ld) {
            LogUtil.Write("Initializing LED Strip.");
            Brightness = ld.Brightness;
            StartupAnimation = ld.StartupAnimation;
            ledCount = ld.VCount * 2 + ld.HCount * 2;
            LogUtil.Write($@"Bright, count, anim: {Brightness}, {ledCount}, {StartupAnimation}");            
            var stripType = rpi_ws281x.WS2812_STRIP;
            neoPixel = new Neopixel(ledCount, ld.PinNumber, stripType);
            LogUtil.Write($@"NeoPixel created using {ledCount} LEDs.");
            neoPixel.Begin();
            Demo();
        }

        private void Demo() {
            for (var i = 0; i < ledCount; i++) {
                var pi = i * 1.0f;
                var progress = pi / ledCount;
                var rCol = Rainbow(progress);
                neoPixel.SetPixelColor(i, rCol);
                neoPixel.Show();
            }
            System.Threading.Thread.Sleep(1000);
            StopLights();
        }
        
        public void UpdateAll(List<Color> colors) {
            var iSource = 0;
            var destArray = new Color[ledCount];
            for (var i = 0; i < ledCount; i++) {
                if (iSource >= colors.Count) {
                    iSource = 0; // reset if at end of source
                }
                neoPixel.SetPixelColor(i, colors[iSource]);
                destArray[i] = colors[iSource++];
                
            }
            neoPixel.Show();
        }

        public void StopLights() {
            var blk = Color.FromArgb(0,0,0, 0);
            LogUtil.Write("Stopping LED Strip.");
            for (var i = 0; i <= ledCount; i++) {
                neoPixel.SetPixelColor(i, blk);
                neoPixel.Show();
            }
            LogUtil.Write("LED Strips stopped.");
        }

        private static Color Rainbow(float progress) {
            var div = Math.Abs(progress % 1) * 6;
            var ascending = (int) (div % 1 * 255);
            var descending = 255 - ascending;
            return (int) div switch {
                0 => Color.FromArgb(255, 255, ascending, 0),
                1 => Color.FromArgb(255, descending, 255, 0),
                2 => Color.FromArgb(255, 0, 255, ascending),
                3 => Color.FromArgb(255, 0, descending, 255),
                4 => Color.FromArgb(255, ascending, 0, 255),
                _ => Color.FromArgb(255, 255, 0, descending)
            };
        }

        #region IDisposable Support
        private bool disposedValue;

        private void Dispose(bool disposing) {
            if (disposedValue) return;
            if (disposing) {
                StopLights();
                neoPixel.Dispose();
            }
            disposedValue = true;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}