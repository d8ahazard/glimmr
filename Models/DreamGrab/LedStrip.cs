using System;
using System.Drawing;
using ws281x.Net;

namespace HueDream.Models.DreamGrab {
    public class LedStrip : IDisposable {
        public int Brightness { get; set; }
        public int StartupAnimation { get; set; }

        private Neopixel neopixel;
        
        public LedStrip(LedData ld) {
            Brightness = ld.Brightness;
            StartupAnimation = ld.StartupAnimation;
            neopixel = new Neopixel(ld.LedCount, ld.PinNumber, ld.StripType);
            neopixel.Begin();
        }

        public void Update() {
            
            var pixelCount = neopixel.GetNumberOfPixels();
            for (var i = 0; i < pixelCount; i++) {
                var progress = i / pixelCount;
                neopixel.SetPixelColor(i, Rainbow(progress));
            }
            neopixel.Show();
        }
        
        public void UpdateAll(Color[] colors) {
            var lCount = 0;
            if (colors != null)
                foreach (var c in colors) {
                    neopixel.SetPixelColor(lCount, c);
                    lCount++;
                }

            neopixel.Show();
        }

        public void StopLights() {
            var pixelCount = neopixel.GetNumberOfPixels();
            for (var i = 0; i < pixelCount; i++) {
                neopixel.SetPixelColor(i, Color.Black);
            }
            neopixel.Show();
            neopixel.Dispose();
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