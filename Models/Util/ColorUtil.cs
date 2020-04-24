using System;
using System.Drawing;

namespace HueDream.Models.Util {
    public static class ColorUtil {
        public static void ColorToHSV(Color color, out double hue, out double saturation, out double value) {
            int max = Math.Max(color.R, Math.Max(color.G, color.B));
            int min = Math.Min(color.R, Math.Min(color.G, color.B));

            hue = color.GetHue();
            saturation = max == 0 ? 0 : 1d - 1d * min / max;
            value = max / 255d;
        }

        public static Color ColorFromHsv(double hue, double saturation, double value) {
            var hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            var f = hue / 60 - Math.Floor(hue / 60);

            value = value * 255;
            var v = Convert.ToInt32(value);
            var p = Convert.ToInt32(value * (1 - saturation));
            var q = Convert.ToInt32(value * (1 - f * saturation));
            var t = Convert.ToInt32(value * (1 - (1 - f) * saturation));

            switch (hi) {
                case 0:
                    return Color.FromArgb(255, v, t, p);
                case 1:
                    return Color.FromArgb(255, q, v, p);
                case 2:
                    return Color.FromArgb(255, p, v, t);
                case 3:
                    return Color.FromArgb(255, p, q, v);
                case 4:
                    return Color.FromArgb(255, t, p, v);
                default:
                    return Color.FromArgb(255, v, p, q);
            }
        }

        public static Color BoostSaturation(Color input, float boost) {
            ColorToHSV(input, out var h, out var s, out var v);
            if (s + boost <= 1.0) {
                s += boost;
            } else {
                s = 1.0;
            }
            return ColorFromHsv(h, s, v);
        }

        public static Color BoostBrightness(Color input, float boost) {
            ColorToHSV(input, out var h, out var s, out var v);
            if (v + boost <= 1.0) {
                v += boost;
            } else {
                v = 1.0;
            }
            return ColorFromHsv(h, s, v);
        }

        public static Color Blend(this Color color, Color backColor, double amount) {
            byte r = (byte) (color.R * amount + backColor.R * (1 - amount));
            byte g = (byte) (color.G * amount + backColor.G * (1 - amount));
            byte b = (byte) (color.B * amount + backColor.B * (1 - amount));
            return Color.FromArgb(r, g, b);
        }
    }
}