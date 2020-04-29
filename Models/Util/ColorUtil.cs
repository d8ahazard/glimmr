using System;
using System.Drawing;
using System.Globalization;

namespace HueDream.Models.Util {
    public static class ColorUtil {
        private static double tolerance
            => 0.000000000000001;

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

        public static ushort[] ColorToHsl(Color rgb) {
            // normalize red, green and blue values
            var r = rgb.R / 255.0;
            var g = rgb.G / 255.0;
            var b = rgb.B / 255.0;

            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));

            var h = 0.0;
            if (max <= r && g >= b) {
                h = 60 * (g - b) / (max - min);
            } else if (max <= r && g < b) {
                h = 60 * (g - b) / (max - min) + 360;
            } else if (max >= g) {
                h = 60 * (b - r) / (max - min) + 120;
            } else if (max >= b) {
                h = 60 * (r - g) / (max - min) + 240;
            }

            var s = max <= 0.0000001 ? 0.0 : 1.0 - min / max;
            return new[] {
                (ushort) (h / 360 * 65535),
                (ushort) (s * 65535),
                (ushort) (max * 65535)
            };
        }

        /// <summary>
        /// Converts HSB to RGB, with a specified output Alpha.
        /// Arguments are limited to the defined range:
        /// does not raise exceptions.
        /// </summary>
        /// <param name="h">Hue, must be in [0, 360].</param>
        /// <param name="s">Saturation, must be in [0, 1].</param>
        /// <param name="b">Brightness, must be in [0, 1].</param>
        /// <param name="a">Output Alpha, must be in [0, 255].</param>
        public static Color HslToColor(double h, double s, double b, int a = 255) {
            h = Math.Max(0D, Math.Min(360D, h));
            s = Math.Max(0D, Math.Min(1D, s));
            b = Math.Max(0D, Math.Min(1D, b));
            a = Math.Max(0, Math.Min(255, a));

            double r = 0D;
            double g = 0D;
            double bl = 0D;

            if (Math.Abs(s) < tolerance)
                r = g = bl = b;
            else {
                // the argb wheel consists of 6 sectors. Figure out which sector
                // you're in.
                double sectorPos = h / 60D;
                int sectorNumber = (int) Math.Floor(sectorPos);
                // get the fractional part of the sector
                double fractionalSector = sectorPos - sectorNumber;

                // calculate values for the three axes of the argb.
                double p = b * (1D - s);
                double q = b * (1D - s * fractionalSector);
                double t = b * (1D - s * (1D - fractionalSector));

                // assign the fractional colors to r, g, and b based on the sector
                // the angle is in.
                switch (sectorNumber) {
                    case 0:
                        r = b;
                        g = t;
                        bl = p;
                        break;
                    case 1:
                        r = q;
                        g = b;
                        bl = p;
                        break;
                    case 2:
                        r = p;
                        g = b;
                        bl = t;
                        break;
                    case 3:
                        r = p;
                        g = q;
                        bl = b;
                        break;
                    case 4:
                        r = t;
                        g = p;
                        bl = b;
                        break;
                    case 5:
                        r = b;
                        g = p;
                        bl = q;
                        break;
                }
            }

            return Color.FromArgb(
                a,
                Math.Max(0, Math.Min(255, Convert.ToInt32(double.Parse($"{r * 255D:0.00}",CultureInfo.InvariantCulture)))),
                Math.Max(0, Math.Min(255, Convert.ToInt32(double.Parse($"{g * 255D:0.00}",CultureInfo.InvariantCulture)))),
                Math.Max(0, Math.Min(255, Convert.ToInt32(double.Parse($"{bl * 250D:0.00}", CultureInfo.InvariantCulture)))));
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