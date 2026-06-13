#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

#endregion

namespace Glimmr.Models.ColorSource.HueSync;

/// <summary>
///     Decodes the binary "HueStream" entertainment frames that the Hue Sync app streams over DTLS.
///     Supports both protocol v1 (per-light, 9 byte records) and v2 (per-channel, 7 byte records).
///     Colors are returned downscaled from 16-bit to 8-bit RGB, indexed by channel/light id.
/// </summary>
public static class EntertainmentFrameParser {
	private static readonly byte[] Magic = Encoding.ASCII.GetBytes("HueStream");

	private const int V2HeaderLength = 52; // 16 byte fixed header + 36 byte entertainment config UUID
	private const int V1HeaderLength = 16;
	private const int V2RecordLength = 7; // channel id (1) + RGB (3 x uint16)
	private const int V1RecordLength = 9; // type (1) + light id (uint16) + RGB (3 x uint16)

	/// <summary>
	///     Color space byte: 0x00 = RGB, 0x01 = XY + brightness.
	/// </summary>
	public static Color[]? Parse(byte[] data) {
		if (data.Length < V1HeaderLength || !StartsWithMagic(data)) {
			return null;
		}

		var major = data[9];
		return major switch {
			2 => ParseV2(data),
			1 => ParseV1(data),
			_ => null
		};
	}

	private static bool StartsWithMagic(byte[] data) {
		for (var i = 0; i < Magic.Length; i++) {
			if (data[i] != Magic[i]) {
				return false;
			}
		}

		return true;
	}

	private static Color[]? ParseV2(byte[] data) {
		if (data.Length < V2HeaderLength) {
			return null;
		}

		var colorSpace = data[14];
		var body = data.Length - V2HeaderLength;
		var count = body / V2RecordLength;
		if (count <= 0) {
			return Array.Empty<Color>();
		}

		var colors = new List<Color>();
		var maxChannel = -1;
		var byChannel = new Dictionary<int, Color>();
		var offset = V2HeaderLength;
		for (var i = 0; i < count; i++) {
			var channel = data[offset];
			var c1 = ReadUInt16(data, offset + 1);
			var c2 = ReadUInt16(data, offset + 3);
			var c3 = ReadUInt16(data, offset + 5);
			byChannel[channel] = ConvertColor(colorSpace, c1, c2, c3);
			if (channel > maxChannel) {
				maxChannel = channel;
			}

			offset += V2RecordLength;
		}

		return FlattenByIndex(byChannel, maxChannel);
	}

	private static Color[]? ParseV1(byte[] data) {
		var colorSpace = data[14];
		var body = data.Length - V1HeaderLength;
		var count = body / V1RecordLength;
		if (count <= 0) {
			return Array.Empty<Color>();
		}

		var maxId = -1;
		var byId = new Dictionary<int, Color>();
		var offset = V1HeaderLength;
		for (var i = 0; i < count; i++) {
			// data[offset] is the device type (0 = light); we only support lights.
			var id = ReadUInt16(data, offset + 1);
			var c1 = ReadUInt16(data, offset + 3);
			var c2 = ReadUInt16(data, offset + 5);
			var c3 = ReadUInt16(data, offset + 7);
			byId[id] = ConvertColor(colorSpace, c1, c2, c3);
			if (id > maxId) {
				maxId = id;
			}

			offset += V1RecordLength;
		}

		return FlattenByIndex(byId, maxId);
	}

	private static Color[] FlattenByIndex(IReadOnlyDictionary<int, Color> byIndex, int maxIndex) {
		var result = new Color[maxIndex + 1];
		for (var i = 0; i <= maxIndex; i++) {
			result[i] = byIndex.TryGetValue(i, out var c) ? c : Color.FromArgb(0, 0, 0);
		}

		return result;
	}

	private static Color ConvertColor(byte colorSpace, ushort c1, ushort c2, ushort c3) {
		if (colorSpace == 0x01) {
			// XY + brightness. c1/c2 are CIE x/y (0..65535 -> 0..1), c3 is brightness.
			var x = c1 / 65535.0;
			var y = c2 / 65535.0;
			var bri = c3 / 65535.0;
			return XyToRgb(x, y, bri);
		}

		// RGB 16-bit -> 8-bit.
		return Color.FromArgb(c1 >> 8, c2 >> 8, c3 >> 8);
	}

	private static ushort ReadUInt16(byte[] data, int offset) {
		return (ushort)((data[offset] << 8) | data[offset + 1]);
	}

	private static Color XyToRgb(double x, double y, double brightness) {
		if (y <= 0) {
			return Color.FromArgb(0, 0, 0);
		}

		var z = 1.0 - x - y;
		var bigY = brightness;
		var bigX = bigY / y * x;
		var bigZ = bigY / y * z;

		var r = bigX * 1.656492 - bigY * 0.354851 - bigZ * 0.255038;
		var g = -bigX * 0.707196 + bigY * 1.655397 + bigZ * 0.036152;
		var b = bigX * 0.051713 - bigY * 0.121364 + bigZ * 1.011530;

		r = GammaCompress(r);
		g = GammaCompress(g);
		b = GammaCompress(b);

		var max = Math.Max(r, Math.Max(g, b));
		if (max > 1.0) {
			r /= max;
			g /= max;
			b /= max;
		}

		return Color.FromArgb(Clamp(r), Clamp(g), Clamp(b));
	}

	private static double GammaCompress(double v) {
		v = Math.Max(0, v);
		return v <= 0.0031308 ? 12.92 * v : 1.055 * Math.Pow(v, 1.0 / 2.4) - 0.055;
	}

	private static int Clamp(double v) {
		var i = (int)Math.Round(v * 255.0);
		return Math.Min(255, Math.Max(0, i));
	}
}
