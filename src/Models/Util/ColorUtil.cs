using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Serilog;

namespace Glimmr.Models.Util {
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

        public static Color ColorFromHex(string input) {
            if (input.Contains("#")) input = input.Trim('#');
            var rs = input.Substring(0, 2);
            var gs = input.Substring(2, 2);
            var bs = input.Substring(4, 2);
            var ri = int.Parse(rs, NumberStyles.HexNumber);
            var gi = int.Parse(gs, NumberStyles.HexNumber);
            var bi = int.Parse(bs, NumberStyles.HexNumber);
            var output = Color.FromArgb(255, ri, gi, bi);
            return output;
        }

        public static List<Color> ClampBrightness(List<Color> input, float maxBrightness) {
            var output = new List<Color>();
            var max = maxBrightness / 255;
            for (var i = 0; i < input.Count; i++) {
	            var c = input[i];
	            var cB = c.GetBrightness();
	            output.Add(cB > max ? HsbToColor(c.GetHue(), c.GetSaturation(), max, c.A) : c);
            }

            return output;
        }

        public static string ColorToHex(Color input) {
            return input.R.ToString("X2") + input.G.ToString("X2") + input.B.ToString("X2");
        }
        
        /// <summary>
        /// Take a n-color list, and convert down to 12 for DS
        /// </summary>
        /// <param name="input">The colors from anywhere else</param>
        /// <returns>12 colors averaged from those, or something.</returns>
        public static List<Color> TruncateColors(List<Color> input) {
	        var indices = input.Count / 12;
	        var output = new List<Color>();
	        for (var i = 0; i < 12; i++) {
		        double idx = i * indices;
		        idx = Math.Floor(idx);
		        if (idx >= input.Count) idx = input.Count - 1;
		        output.Add(input[(int)idx]);
	        }
            return output;
        }
		
		
        /// <summary>
        /// Return the average of inputted colors
        /// </summary>
        /// <param name="colors"></param>
        /// <returns></returns>
        public static Color AverageColors(params Color[] colors) {
            var inputCount = colors.Length;
            if (inputCount == 0) return Color.FromArgb(0, 0, 0, 0);
            var avgG = 0;
            var avgB = 0;
            var avgR = 0;
            var avgA = 0;
            for (var i = 0; i < colors.Length; i++) {
	            var t = colors[i];
	            avgG += t.G * t.G;
	            avgB += t.B * t.B;
	            avgR += t.R * t.R;
	            avgA += t.A * t.A;
            }

            avgG /= inputCount;
            avgB /= inputCount;
            avgR /= inputCount;
            avgA /= inputCount;
            return Color.FromArgb((int)Math.Sqrt(avgA), (int)Math.Sqrt(avgR), (int) Math.Sqrt(avgB), (int) Math.Sqrt(avgG));
        }

        public static Color ClampAlpha(Color tCol) {
            var rI = tCol.R;
            var gI = tCol.G;
            var bI = tCol.B;
            float tM = Math.Max(rI, Math.Max(gI, bI));
            float tm = Math.Min(rI, Math.Min(gI, bI));
            //If the maximum value is 0, immediately return pure black.
            if(tM == 0) { return Color.FromArgb(0, 0, 0,0); }
            
            if (tm >= 255) {return Color.FromArgb(255, 0, 0, 0);}

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

        
        public static Color ClampAlpha2(Color tCol) {
            var rI = tCol.R;
            var gI = tCol.G;
            var bI = tCol.B;
            int tM = Math.Max(rI, Math.Max(gI, bI));
            int tm = Math.Min(rI, Math.Min(gI, bI));
            //If the maximum value is 0, immediately return pure black.
            //if(tM == 0) { return Color.FromArgb(0, 0, 0,0); }
            if (tm >= 255 && rI + gI + bI >= 255) {
                tm = 128;
            }
            return Color.FromArgb(tm, rI, gI, bI);
        }

        public static int ColorTemperature(Color input) {
            // Get difference between red and blue
            var diff = input.B - input.R;
            // Pad it +255 to adjust to 0
            diff += 255;
            // What percentage is it?
            return diff / 255 * 13500 + 1500;
            
        }
        
        public static Color FromAhsb(int alpha, float hue, float saturation, float brightness)
		{
			if (0 > alpha
			    || 255 < alpha)
			{
				throw new ArgumentOutOfRangeException(
					"alpha",
					alpha,
					"Value must be within a range of 0 - 255.");
			}

			if (0f > hue
			    || 360f < hue)
			{
				throw new ArgumentOutOfRangeException(
					"hue",
					hue,
					"Value must be within a range of 0 - 360.");
			}

			if (0f > saturation
			    || 1f < saturation)
			{
				throw new ArgumentOutOfRangeException(
					"saturation",
					saturation,
					"Value must be within a range of 0 - 1.");
			}

			if (0f > brightness
			    || 1f < brightness)
			{
				throw new ArgumentOutOfRangeException(
					"brightness",
					brightness,
					"Value must be within a range of 0 - 1.");
			}

			if (0 == saturation)
			{
				return Color.FromArgb(
					alpha,
					Convert.ToInt32(brightness * 255),
					Convert.ToInt32(brightness * 255),
					Convert.ToInt32(brightness * 255));
			}

			float fMax, fMid, fMin;
			int iSextant, iMax, iMid, iMin;

			if (0.5 < brightness)
			{
				fMax = brightness - brightness * saturation + saturation;
				fMin = brightness + brightness * saturation - saturation;
			}
			else
			{
				fMax = brightness + brightness * saturation;
				fMin = brightness - brightness * saturation;
			}

			iSextant = (int)Math.Floor(hue / 60f);
			if (300f <= hue)
			{
				hue -= 360f;
			}

			hue /= 60f;
			hue -= 2f * (float)Math.Floor((iSextant + 1f) % 6f / 2f);
			if (0 == iSextant % 2)
			{
				fMid = hue * (fMax - fMin) + fMin;
			}
			else
			{
				fMid = fMin - hue * (fMax - fMin);
			}

			iMax = Convert.ToInt32(fMax * 255);
			iMid = Convert.ToInt32(fMid * 255);
			iMin = Convert.ToInt32(fMin * 255);

			switch (iSextant)
			{
				case 1:
					return Color.FromArgb(alpha, iMid, iMax, iMin);
				case 2:
					return Color.FromArgb(alpha, iMin, iMax, iMid);
				case 3:
					return Color.FromArgb(alpha, iMin, iMid, iMax);
				case 4:
					return Color.FromArgb(alpha, iMid, iMin, iMax);
				case 5:
					return Color.FromArgb(alpha, iMax, iMin, iMid);
				default:
					return Color.FromArgb(alpha, iMax, iMid, iMin);
			}
		}
       
        /// <summary>
        /// Convert HSV values to color
        /// </summary>
        /// <param name="hue">0-360</param>
        /// <param name="saturation">0-1</param>
        /// <param name="value">0-1</param>
        /// <returns></returns>
        public static Color HsvToColor(double hue, double saturation, double value) {
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

        public static Color SetBrightness(Color input, float brightness) {
	        var hsb = ColorToHsb(input);
	        if (input.R == 0 && input.G == 0 && input.B == 0) return input;
	        return HsbToColor(hsb[0], hsb[1], brightness);
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

            return HsvToColor(h, s, v);
        }

        public static Color[] FillArray(Color input, int len) {
	        var output = new Color[len];
	        for (var i = 0; i < len; i++) {
		        output[i] = input;
	        }
	        return output;
        }
        
        public static bool IsBlack(Color color, int min = 5) {
            return color.R < min && color.G < min && color.B < min;
        }

        public static Color AdjustBrightness(Color input, float boost) {
            ColorToHSV(input, out var h, out var s, out var v);
            if (v + boost <= 1.0) {
                v += boost;
                //s -= boost;
            } else {
                v = 1.0;
            }

            return HsvToColor(h, s, v);
        }
        
        public static Color Rainbow(float progress) {
            var div = Math.Abs(progress % 1) * 6;
            var ascending = (int) (div % 1 * 255);
            var descending = 255 - @ascending;
            var alpha = 0;
            return (int) div switch {
                0 => Color.FromArgb(alpha, 255, @ascending, 0),
                1 => Color.FromArgb(alpha, @descending, 255, 0),
                2 => Color.FromArgb(alpha, 0, 255, @ascending),
                3 => Color.FromArgb(alpha, 0, @descending, 255),
                4 => Color.FromArgb(alpha, @ascending, 0, 255),
                _ => Color.FromArgb(alpha, 255, 0, @descending)
            };
        }
        
        public static Color[] EmptyColors(Color[] input) {
            for (var i = 0; i < input.Length; i++) {
                input[i] = Color.FromArgb(0, 0, 0, 0);
            }

            return input;
        }
        
        public static List<Color> EmptyList(int size) {
	        var output = new List<Color>();
	        for (var i = 0; i < size; i++) {
		        output.Add(Color.FromArgb(0, 0, 0, 0));
	        }
	        return output;
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
            
            return Color.FromArgb(gammas[input.A], gammas[input.R], gammas[input.G], gammas[input.B]);
        }

        public static Color FixGamma2(Color input) {
            var w = ByteUtils.IntByte(input.A) >> 24;
            var r = ByteUtils.IntByte(input.R) >> 16;
            var g = ByteUtils.IntByte(input.G) >> 8;
            var b = input.B;
            var shifted = Color.FromArgb(w, r, g, b);
            shifted = FixGamma(shifted);
            return Color.FromArgb(shifted.A << 24, shifted.R << 16, shifted.G << 8, shifted.B);
        }

        public static Color Blend(this Color color, Color backColor, double amount) {
            byte r = (byte) (color.R * amount + backColor.R * (1 - amount));
            byte g = (byte) (color.G * amount + backColor.G * (1 - amount));
            byte b = (byte) (color.B * amount + backColor.B * (1 - amount));
            return Color.FromArgb(r, g, b);
        }

        public static float HueFromFrequency(int frequency) {
	        
	        var start = 16;
	        if (frequency < start) frequency = start;
	        var end = start * 2;
	        const int max = 10000;
	        while (end < frequency && start < max) {
		        start *= 2;
		        end *= 2;
	        }
	        if (frequency >= start) {
		        var pct = (float) (frequency - start) / start;
		        return pct;
	        }

	        return 1;
        }

        public static List<Color> SectorsToleds(List<Color> ints, SystemData sd) {
	        var output = new List<Color>();
	        // We're going to duplicate the "corner" color values so they evenly map to LED colors
	        var shifted = new List<Color>();
	        var ledCount = sd.LedCount;
	        var tr = sd.VSectors - 1;
	        var tl = tr + sd.HSectors - 1;
	        var bl = tl + sd.VSectors - 1;
	        for (var i = 0; i < ints.Count; i++) {
		        shifted.Add(ints[i]);
		        if (i == tr || i == tl || i == bl) shifted.Add(ints[i]);
	        }
			shifted.Add(ints[0]);
			
			// How many LEDs per shifted sector?
			var step = (int) Math.Floor(ledCount / shifted.Count * 1.0f);
			var sector = 0;
	        for (var i=0; i < ledCount; i++) {
		        if (i % step == 0) {
			        sector++;
		        }

		        if (sector >= ints.Count) sector = ints.Count - 1;
		        output.Add(ints[sector]);
	        }

	        return output;
        }

        public static Color[] AddLedColor(Color[] colors, int sector, Color color, SystemData systemData) {
	        int s0;
	        int e0;
			
	        int vs = systemData.VSectors;
	        int hs = systemData.HSectors;
	        var count = systemData.LeftCount + systemData.RightCount + systemData.TopCount + systemData.BottomCount;
	        var rCount = systemData.RightCount / vs;
	        var tCount = systemData.TopCount / hs;
	        var lCount = systemData.LeftCount / vs;
	        var bCount = systemData.BottomCount / hs;

	        var rightLimit = vs;
	        var topLimit = vs + hs - 1;
	        var leftLimit = vs + hs + vs - 2;
	        var bottomLimit = vs + hs + vs + hs - 3;

	        if (sector >= 1 && sector <= rightLimit) {
		        e0 = sector * rCount;
		        s0 = e0 - rCount;
		        for (var i = s0; i < e0; i++) {
			        if (i < colors.Length) colors[i] = color;
		        }
	        }

	        // Top leds
	        if (sector >= leftLimit && sector <= topLimit ) {
		        var sec = sector - leftLimit;
		        e0 = sec * tCount;
		        e0 += systemData.LeftCount;
		        s0 = e0 - tCount;
		        for (var i = s0; i < e0; i++) {
			        if (i < colors.Length) colors[i] = color;
		        }
	        }

	        // Left leds
	        if (sector >= topLimit && sector <= rightLimit) {
		        var sec = sector - topLimit;
		        e0 = sec * lCount;
		        e0 += systemData.RightCount + systemData.TopCount;
		        s0 = e0 - lCount;
		        for (var i = s0; i < e0; i++) {
			        if (i < colors.Length) colors[i] = color;
		        }
	        }

	        // Bottom leds
	        if (sector >= rightLimit && sector <= bottomLimit) {
		        var sec = sector - rightLimit;
		        e0 = sec * bCount;
		        e0 += systemData.RightCount + systemData.LeftCount + systemData.TopCount;
		        s0 = e0 - bCount;
		        for (var i = s0; i < e0; i++) {
			        if (i < colors.Length) colors[i] = color;
		        }
	        }

	        // Also bottom
	        if (sector == 1) {
		        s0 = count - bCount;
		        e0 = count;
		        for (var i = s0; i < e0; i++) {
			        if (i < colors.Length) colors[i] = color;
		        }
	        }

	        return colors;
        }

        public static List<Color> LedsToSectors(List<Color> ledColors, SystemData sd) {
	        var rightColors = ledColors.GetRange(0, sd.RightCount);
	        var topColors = ledColors.GetRange(sd.RightCount-1, sd.TopCount);
	        var leftColors = ledColors.GetRange(sd.TopCount - 1, sd.LeftCount);
	        var bottomColors = ledColors.GetRange(sd.LeftCount - 1, sd.BottomCount);
	        float rStep = rightColors.Count / sd.VSectors;
	        float tStep = topColors.Count / sd.HSectors;
	        float lStep = leftColors.Count / sd.VSectors;
	        float bStep = bottomColors.Count / sd.HSectors;
	        var output = new List<Color>();
	        var toAvg = new List<Color>();
	        // Add the last range of colors from the bottom to sector 0
	        for(var i=bottomColors.Count - 1 - bStep; i < bottomColors.Count; i++) toAvg.Add(bottomColors[(int)i]);
	        var idx = 0;
	        while (idx < rightColors.Count && output.Count <= sd.VSectors) {
		        foreach (var t in rightColors)
			        toAvg.Add(t);
				
		        // On the last sector, don't average it so we can add the bit from the next corner
		        if (idx % lStep == 0 && output.Count < sd.VSectors) {
			        output.Add(AverageColors(toAvg.ToArray()));
			        toAvg = new List<Color>();
		        }
		        idx++;
	        }
	        
	        idx = 0;
	        while (idx < topColors.Count && output.Count < sd.VSectors + sd.HSectors + 1) {
		        foreach (var t in topColors)
			        toAvg.Add(t);

		        if (idx % lStep == 0 && output.Count < sd.VSectors + sd.HSectors + 1) {
			        output.Add(AverageColors(toAvg.ToArray()));
			        toAvg = new List<Color>();
		        }
		        idx++;
	        }
	        
	        idx = 0;
	        while (idx < leftColors.Count && output.Count < sd.VSectors + sd.HSectors + sd.VSectors - 2) {
		        foreach (var t in leftColors)
			        toAvg.Add(t);

		        if (idx % lStep == 0 && output.Count < 22) {
			        output.Add(AverageColors(toAvg.ToArray()));
			        toAvg = new List<Color>();
		        }
		        idx++;
	        }
	        
	        idx = 0;
	        while (idx < bottomColors.Count && output.Count < sd.VSectors + sd.HSectors + sd.VSectors + sd.HSectors - 4) {
		        foreach (var t in leftColors)
			        toAvg.Add(t);

		        if (idx % lStep == 0) {
			        output.Add(AverageColors(toAvg.ToArray()));
			        toAvg = new List<Color>();
		        }
		        idx++;
	        }

	        return output;
        }

        /// <summary>
        /// Multiplies the Color's Luminance or Brightness by the argument;
        /// and optionally specifies the output Alpha.
        /// </summary>
        /// <param name="color">The color to transform.</param>
        public static Color ClampBrightness(Color color, double maxBrightness) {
	        double bClamp = maxBrightness * .01;
	        double[] hsl = RgBtoHsb(color);
	        if (Math.Abs(hsl[2]) > bClamp) {
		        hsl[2] = bClamp;
		        return HsBtoRgb(hsl[0], hsl[1], hsl[2], color.A);
	        }
	        return color;
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
        public static Color HsBtoRgb(double h, double s, double b, int a = 255) {
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
		        Math.Max(0, Math.Min(255, Convert.ToInt32(double.Parse($"{r * 255D:0.00}")))),
		        Math.Max(0, Math.Min(255, Convert.ToInt32(double.Parse($"{g * 255D:0.00}")))),
		        Math.Max(0, Math.Min(255, Convert.ToInt32(double.Parse($"{bl * 250D:0.00}")))));
        }

        /// <summary>
        /// Converts RGB to HSB. Alpha is ignored.
        /// Output is: { H: [0, 360], S: [0, 1], B: [0, 1] }.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        public static double[] RgBtoHsb(Color color) {
	        // normalize red, green and blue values
	        double r = color.R / 255D;
	        double g = color.G / 255D;
	        double b = color.B / 255D;

	        // conversion start
	        double max = Math.Max(r, Math.Max(g, b));
	        double min = Math.Min(r, Math.Min(g, b));

	        double h = 0D;
	        if (Math.Abs(max - r) < tolerance
	            && g >= b)
		        h = 60D * (g - b) / (max - min);
	        else if (Math.Abs(max - r) < tolerance
	                 && g < b)
		        h = 60D * (g - b) / (max - min) + 360D;
	        else if (Math.Abs(max - g) < tolerance)
		        h = 60D * (b - r) / (max - min) + 120D;
	        else if (Math.Abs(max - b) < tolerance)
		        h = 60D * (r - g) / (max - min) + 240D;

	        double s = Math.Abs(max) < tolerance
		        ? 0D
		        : 1D - min / max;

	        return new[] {
		        Math.Max(0D, Math.Min(360D, h)),
		        Math.Max(0D, Math.Min(1D, s)),
		        Math.Max(0D, Math.Min(1D, max))
	        };
        }
    }
    
    

    public static class ColorExtension {  
        public static Color FromString(this Color color, string str) {
            return ColorUtil.ColorFromHex(str);
        }

        public static string ToString(this Color color) {
            return ColorUtil.ColorToHex(color);
        }
    }  
}