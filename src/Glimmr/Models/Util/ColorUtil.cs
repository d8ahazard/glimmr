#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Glimmr.Models.Data;

#endregion

namespace Glimmr.Models.Util;

public static class ColorUtil {
	/// <summary>
	///     Take a n-color list, and convert down to 12 for DS
	/// </summary>
	/// <param name="input">The colors from anywhere else</param>
	/// <returns>12 colors averaged from those, or something.</returns>
	public static Color[] TruncateColors(Color[] input) {
		var indices = input.Length / 12;
		var output = EmptyColors(12);
		for (var i = 0; i < 12; i++) {
			double idx = i * indices;
			idx = Math.Floor(idx);
			if (idx >= input.Length) {
				idx = input.Length - 1;
			}

			output[i] = input[(int)idx];
		}

		return output;
	}

	public static Color[] TruncateColors(Color[] input, int offset, int len, float multiplier = 1f) {
		// Normalize the offset to ensure it's within the bounds of the input array
		offset = (offset % input.Length + input.Length) % input.Length;

		var output = new Color[len];
		for (var i = 0; i < len; i++) {
			// Calculate the target index based on offset, multiplier, and loop index
			// Use modulo to ensure the index is within the bounds of the input array
			var tgt = (int)((offset + i * multiplier) % input.Length);
			output[i] = input[tgt];
		}

		return output;
	}



	/// <summary>
	///     Return the average of inputted colors
	/// </summary>
	/// <param name="colors"></param>
	/// <returns></returns>
	private static Color AverageColors(params Color[] colors) {
		if (colors.Length == 0) {
			return Color.FromArgb(0, 0, 0, 0);
		}

		int sumR = 0, sumG = 0, sumB = 0, sumA = 0;
		foreach (var color in colors) {
			sumR += color.R;
			sumG += color.G;
			sumB += color.B;
			sumA += color.A;
		}

		int count = colors.Length;
		return Color.FromArgb(
			sumA / count,
			sumR / count,
			sumG / count,
			sumB / count
		);
	}


	public static Color ClampAlpha(Color tCol) {
		// Relative luminance formula for sRGB colors
		var luminance = 0.2126f * tCol.R + 0.7152f * tCol.G + 0.0722f * tCol.B;

		// Convert luminance to an integer and clamp it between 0 and 255
		var wO = (int)Math.Max(0, Math.Min(255, luminance));

		// Subtract luminance from each color component and clamp the result
		var rO = (int)Math.Max(0, Math.Min(255, tCol.R - luminance));
		var gO = (int)Math.Max(0, Math.Min(255, tCol.G - luminance));
		var bO = (int)Math.Max(0, Math.Min(255, tCol.B - luminance));

		// Return the adjusted color
		return Color.FromArgb(wO, rO, gO, bO);
	}



	/// <summary>
	///     Convert HSV values to color
	/// </summary>
	/// <param name="hue">0-360</param>
	/// <param name="saturation">0-1</param>
	/// <param name="value">0-1</param>
	/// <returns></returns>
	public static Color HsvToColor(double hue, double saturation, double value) {
		var hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
		var f = hue / 60 - Math.Floor(hue / 60);

		value *= 255;
		var v = Convert.ToInt32(value);
		var p = Convert.ToInt32(value * (1 - saturation));
		var q = Convert.ToInt32(value * (1 - f * saturation));
		var t = Convert.ToInt32(value * (1 - (1 - f) * saturation));

		return hi switch {
			0 => Color.FromArgb(255, v, t, p),
			1 => Color.FromArgb(255, q, v, p),
			2 => Color.FromArgb(255, p, v, t),
			3 => Color.FromArgb(255, p, q, v),
			4 => Color.FromArgb(255, t, p, v),
			_ => Color.FromArgb(255, v, p, q)
		};
	}

	public static Color SetBrightness(Color color, float brightness) {
		// Ensure brightness is within the valid range [0, 1]
		brightness = Math.Max(0, Math.Min(brightness, 1));

		// Convert RGB to HSL
		var (h, s, l) = RgbToHsl(color);

		// Set the new brightness (lightness in HSL)
		l = brightness;

		// Convert HSL back to RGB
		return HslToRgb(h, s, l);
	}

	private static (float h, float s, float l) RgbToHsl(Color color) {
		var r = color.R / 255f;
		var g = color.G / 255f;
		var b = color.B / 255f;
		var max = Math.Max(r, Math.Max(g, b));
		var min = Math.Min(r, Math.Min(g, b));
		float h, s, l;
		l = (max + min) / 2;

		if (max == min) {
			h = s = 0; // achromatic
		} else {
			var d = max - min;
			s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
			if (max == r) {
				h = (g - b) / d + (g < b ? 6 : 0);
			} else if (max == g) {
				h = (b - r) / d + 2;
			} else {
				h = (r - g) / d + 4;
			}
			h /= 6;
		}

		return (h, s, l);
	}

	private static Color HslToRgb(float h, float s, float l) {
		float r, g, b;

		if (s == 0) {
			r = g = b = l; // achromatic
		} else {
			float Hue2Rgb(float p, float q, float t) {
				if (t < 0) t += 1;
				if (t > 1) t -= 1;
				return t switch {
					< 1 / 6f => p + (q - p) * 6 * t,
					< 1 / 2f => q,
					< 2 / 3f => p + (q - p) * (2 / 3f - t) * 6,
					_ => p
				};
			}

			var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
			var p = 2 * l - q;
			r = Hue2Rgb(p, q, h + 1 / 3f);
			g = Hue2Rgb(p, q, h);
			b = Hue2Rgb(p, q, h - 1 / 3f);
		}

		return Color.FromArgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
	}



	public static Color[] FillArray(Color input, int len) {
		var output = new Color[len];
		for (var i = 0; i < len; i++) {
			output[i] = input;
		}

		return output;
	}

	public static Color Rainbow(float progress) {
		var div = Math.Abs(progress % 1) * 6;
		var ascending = (int)(div % 1 * 255);
		var descending = 255 - ascending;
		const int alpha = 0;
		return (int)div switch {
			0 => Color.FromArgb(alpha, 255, ascending, 0),
			1 => Color.FromArgb(alpha, descending, 255, 0),
			2 => Color.FromArgb(alpha, 0, 255, ascending),
			3 => Color.FromArgb(alpha, 0, descending, 255),
			4 => Color.FromArgb(alpha, ascending, 0, 255),
			_ => Color.FromArgb(alpha, 255, 0, descending)
		};
	}

	public static Color[] EmptyColors(int len) {
		var output = new Color[len];
		var empty = Color.FromArgb(0, 0, 0, 0);
		for (var i = 0; i < len; i++) {
			output[i] = empty;
		}

		return output;
	}

	public static List<Color> EmptyList(int size) {
		var output = new List<Color>(size);
		for (var i = 0; i < size; i++) {
			output.Add(Color.FromArgb(0, 0, 0, 0));
		}

		return output;
	}

	public static byte[] GammaTable(float gamma) {
		var GammaCorrection = new byte[256];
		var logBS = new int[256];
		for (var i = 0; i < 256; i++) {
			GammaCorrection[i] = (byte)i;
			logBS[i] = i;
		}

		if (!(gamma > 1.0f)) {
			return GammaCorrection;
		}

		{
			for (var i = 0; i < 256; i++) {
				GammaCorrection[i] = (byte)(Math.Pow(i / (float)255, gamma) * 255 + 0.5);
				logBS[i] = GammaCorrection[i];
			}
		}

		return GammaCorrection;
	}

	public static float HueFromFrequency(float frequency, int octave) {
		var start = new[] { 16.35f, 32.7f, 65.41f, 130.81f, 261.63f, 523.25f, 1046.5f, 2093f, 4186.01f };
		var end = new[] { 30.87f, 61.74f, 123.47f, 246.94f, 493.88f, 987.77f, 1975.53f, 3951.07f, 7902.13f };
		var minFrequency = start[octave];
		var maxFrequency = end[octave];

		var range = maxFrequency - minFrequency;
		var location = frequency / range;
		return location;
	}

	public static List<Color> LedsToSectors(List<Color> ledColors, SystemData sd) {
        var output = new List<Color>();
    
        // Helper function to add averaged colors from a section to the output
        void AddAveragedColors(int start, int count, float step, int maxSectors) {
            var toAvg = new List<Color>();
            for (var i = start; i < Math.Min(start + count, ledColors.Count) && output.Count < maxSectors; i++) {
                toAvg.Add(ledColors[i]);
                if ((i - start) % step == 0) {
                    output.Add(AverageColors(toAvg.ToArray()));
                    toAvg.Clear();
                }
            }
            // Add any remaining colors
            if (toAvg.Any()) {
                output.Add(AverageColors(toAvg.ToArray()));
            }
        }
    
        // Right section
        AddAveragedColors(0, sd.RightCount, (float)sd.RightCount / sd.VSectors, sd.VSectors);
    
        // Top section
        AddAveragedColors(sd.RightCount - 1, sd.TopCount, (float)sd.TopCount / sd.HSectors, sd.VSectors + sd.HSectors - 1);
    
        // Left section
        AddAveragedColors(sd.TopCount - 1, sd.LeftCount, (float)sd.LeftCount / sd.VSectors, sd.VSectors * 2 + sd.HSectors - 2);
    
        // Bottom section, starting from the last color to include the bit from the next corner
        AddAveragedColors(ledColors.Count - sd.BottomCount, sd.BottomCount, (float)sd.BottomCount / sd.HSectors, sd.SectorCount);
    
        return output;
    }



	/// <summary>
	///     Adjust the brightness of a list of colors
	/// </summary>
	/// <param name="toSend">Input colors</param>
	/// <param name="max">A float from 0-1, representing the max percentage brightness can be represented.</param>
	/// <returns></returns>
	public static Color[] AdjustBrightness(Color[] toSend, float max) {
		var output = new Color[toSend.Length];
		var mc = (byte)(max * 255f);
		for (var i = 0; i < toSend.Length; i++) {
			var color = toSend[i];
			// Max value is the brightness
			var colM = Math.Max(color.R, Math.Max(color.G, color.B));
			// Divide by 255 and multiply by max we can have...
			var cMax = (byte)(colM / 255f * mc);
			var diff = 0;
			if (colM > cMax) {
				diff = colM - cMax;
			}

			var r = Math.Max(0, color.R - diff);
			var g = Math.Max(0, color.G - diff);
			var b = Math.Max(0, color.B - diff);
			output[i] = Color.FromArgb(r, g, b);
		}

		return output;
	}

	public static Color ClampBrightness(Color col, int dataBrightness) {
		var max = Math.Max(col.R, Math.Max(col.G, col.B));
		if (max <= dataBrightness) {
			return col;
		}

		var diff = max - dataBrightness;
		var r = Math.Max(col.R - diff, 0);
		var g = Math.Max(col.G - diff, 0);
		var b = Math.Max(col.B - diff, 0);
		return Color.FromArgb(r, g, b);
	}
}