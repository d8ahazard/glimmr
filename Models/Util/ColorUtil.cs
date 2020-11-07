using System;
using System.Drawing;
using System.Globalization;
using Newtonsoft.Json;
using Q42.HueApi.ColorConverters;
using Q42.HueApi.ColorConverters.HSB;

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

        public static Color ClampAlpha(Color tCol) {
            var rI = tCol.R;
            var gI = tCol.G;
            var bI = tCol.B;
            float tM = Math.Max(rI, Math.Max(gI, bI));

            //If the maximum value is 0, immediately return pure black.
            if(tM == 0) { return Color.FromArgb(0, 0, 0,0); }

            //This section serves to figure out what the color with 100% hue is
            var multiplier = 255.0f / tM;
            var hR = rI * multiplier;
            var hG = gI * multiplier;
            var hB = bI * multiplier;  

            //This calculates the Whiteness (not strictly speaking Luminance) of the color
            var maxWhite = Math.Max(hR, Math.Max(hG, hB));
            var minWhite = Math.Min(hR, Math.Min(hG, hB));
            var luminance = ((maxWhite + minWhite) / 2.0f - 127.5f) * (255.0f/127.5f) / multiplier;

            //Calculate the output values
            var wO = Convert.ToInt32(luminance);
            var bO = Convert.ToInt32(bI - luminance);
            var rO = Convert.ToInt32(rI - luminance);
            var gO = Convert.ToInt32(gI - luminance);

            //Trim them so that they are all between 0 and 255
            if (wO < 0) wO = 0;
            if (bO < 0) bO = 0;
            if (rO < 0) rO = 0;
            if (gO < 0) gO = 0;
            if (wO > 255) wO = 255;
            if (bO > 255) bO = 255;
            if (rO > 255) rO = 255;
            if (gO > 255) gO = 255;
            return Color.FromArgb(wO, rO, gO, bO);
        }

        public static int ColorTemperature(Color input) {
            // Get difference between red and blue
            var diff = input.B - input.R;
            // Pad it +255 to adjust to 0
            diff += 255;
            // What percentage is it?
            return diff / 255 * 13500 + 1500;
            
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

        public static double[] ColorToHsb(Color rgb) {
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
                h,
                s,
                max
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
        public static Color HsbToColor(double h, double s, double b, int a = 255) {
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
        
        public static bool IsBlack(Color color, int min = 5) {
            return color.R < min && color.G < min && color.B < min;
        }

        public static Color BoostBrightness(Color input, float boost) {
            ColorToHSV(input, out var h, out var s, out var v);
            if (v + boost <= 1.0) {
                v += boost;
                s -= boost;
            } else {
                v = 1.0;
            }

            return ColorFromHsv(h, s, v);
        }

        public static Color FixGamma(Color input) {
            int[] gammas = {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1,
                1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2,
                2, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 5, 5, 5,
                5, 6, 6, 6, 6, 7, 7, 7, 7, 8, 8, 8, 9, 9, 9, 10,
                10, 10, 11, 11, 11, 12, 12, 13, 13, 13, 14, 14, 15, 15, 16, 16,
                17, 17, 18, 18, 19, 19, 20, 20, 21, 21, 22, 22, 23, 24, 24, 25,
                25, 26, 27, 27, 28, 29, 29, 30, 31, 32, 32, 33, 34, 35, 35, 36,
                37, 38, 39, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 50,
                51, 52, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 66, 67, 68,
                69, 70, 72, 73, 74, 75, 77, 78, 79, 81, 82, 83, 85, 86, 87, 89,
                90, 92, 93, 95, 96, 98, 99,101,102,104,105,107,109,110,112,114,
                115,117,119,120,122,124,126,127,129,131,133,135,137,138,140,142,
                144,146,148,150,152,154,156,158,160,162,164,167,169,171,173,175,
                177,180,182,184,186,189,191,193,196,198,200,203,205,208,210,213,
                215,218,220,223,225,228,231,233,236,239,241,244,247,249,252,255 };
            
            return Color.FromArgb(input.A, gammas[input.R], gammas[input.G], gammas[input.B]);
        }

        public static Color Blend(this Color color, Color backColor, double amount) {
            byte r = (byte) (color.R * amount + backColor.R * (1 - amount));
            byte g = (byte) (color.G * amount + backColor.G * (1 - amount));
            byte b = (byte) (color.B * amount + backColor.B * (1 - amount));
            return Color.FromArgb(r, g, b);
        }
    }
}