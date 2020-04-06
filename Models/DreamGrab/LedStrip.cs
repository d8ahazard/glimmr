using HueDream.Models.Util;
using System;
using System.Drawing;
using ws281x.Net;

namespace HueDream.Models.DreamGrab {
    public class LedStrip : IDisposable {
        public int Brightness { get; set; }
        public int StartupAnimation { get; set; }

        private int ledCount;

        private Neopixel neopixel;
        
        public LedStrip(LedData ld) {
            LogUtil.Write("Initializing LED Strip.");
            Brightness = ld.Brightness;
            StartupAnimation = ld.StartupAnimation;
            ledCount = ld.VCount * 2 + ld.HCount * 2;
            LogUtil.Write($@"Bright, count, anim: {Brightness}, {ledCount}, {StartupAnimation}");            
            var stripType = rpi_ws281x.WS2812_STRIP;
            LogUtil.Write("Read variables, wtf...");
            neopixel = new Neopixel(ld.LedCount, ld.PinNumber, stripType);
            LogUtil.Write("Neopixel created using " + ledCount + "leds.");
            neopixel.Begin();
            Demo();
        }

        public void Demo() {
            var pixelCount = neopixel.GetNumberOfPixels();
            for (var i = 0; i < pixelCount; i++) {
                var progress = i / pixelCount;
                neopixel.SetPixelColor(i, Rainbow(progress));
            }
            neopixel.Show();
        }
        
        public void UpdateAll(Color[] colors) {
            var iSource = 0;
            var destArray = new Color[ledCount];
            for (var i = 0; i < ledCount; i++) {
                if (iSource >= colors.Length) {
                    iSource = 0; // reset if at end of source
                }
                neopixel.SetPixelColor(i, colors[iSource]);
                destArray[i] = colors[iSource++];
            }
            neopixel.Show();
        }

        public void StopLights() {
            LogUtil.Write("Stopping LED Strip.");
            var pixelCount = neopixel.GetNumberOfPixels();
            for (var i = 0; i < pixelCount; i++) {
                neopixel.SetPixelColor(i, Color.Black);
            }
            neopixel.Show();
            LogUtil.Write("LED Strips stopped.");
            //neopixel.Dispose();
        }

        public static Color Rainbow(float progress)
        {
            float div = Math.Abs(progress % 1) * 6;
            int ascending = (int) (div % 1 * 255);
            int descending = 255 - ascending;

            switch ((int) div)
            {
                case 0:
                    return Color.FromArgb(255, 255, ascending, 0);
                case 1:
                    return Color.FromArgb(255, descending, 255, 0);
                case 2:
                    return Color.FromArgb(255, 0, 255, ascending);
                case 3:
                    return Color.FromArgb(255, 0, descending, 255);
                case 4:
                    return Color.FromArgb(255, ascending, 0, 255);
                default: // case 5:
                    return Color.FromArgb(255, 255, 0, descending);
            }
        }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    StopLights();
                    neopixel.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}