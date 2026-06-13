using System.Text;

namespace HueSyncTestClient;

/// <summary>
///     Builds binary "HueStream" v2 entertainment frames with animated colors so the receiving end
///     visibly drives lights. Channel i is given a hue offset and the whole field rotates over time.
/// </summary>
public static class HueStreamFrame {
	private static readonly byte[] Magic = Encoding.ASCII.GetBytes("HueStream");

	public static byte[] BuildV2(string entertainmentId, int channelCount, int frameIndex) {
		var idBytes = Encoding.ASCII.GetBytes(entertainmentId.PadRight(36).Substring(0, 36));

		using var ms = new MemoryStream();
		ms.Write(Magic, 0, Magic.Length); // 0-8
		ms.WriteByte(0x02); // major version
		ms.WriteByte(0x00); // minor version
		ms.WriteByte((byte)(frameIndex & 0xFF)); // sequence id
		ms.WriteByte(0x00); // reserved
		ms.WriteByte(0x00); // reserved
		ms.WriteByte(0x00); // color space: RGB
		ms.WriteByte(0x00); // reserved
		ms.Write(idBytes, 0, idBytes.Length); // 36-byte entertainment configuration id

		for (var ch = 0; ch < channelCount; ch++) {
			var hue = (frameIndex * 2 + ch * (360.0 / Math.Max(1, channelCount))) % 360.0;
			var (r, g, b) = HsvToRgb16(hue, 1.0, 1.0);
			ms.WriteByte((byte)ch);
			WriteUInt16(ms, r);
			WriteUInt16(ms, g);
			WriteUInt16(ms, b);
		}

		return ms.ToArray();
	}

	private static void WriteUInt16(Stream s, ushort value) {
		s.WriteByte((byte)(value >> 8));
		s.WriteByte((byte)(value & 0xFF));
	}

	private static (ushort r, ushort g, ushort b) HsvToRgb16(double h, double sat, double val) {
		var c = val * sat;
		var x = c * (1 - Math.Abs(h / 60.0 % 2 - 1));
		var m = val - c;
		double r, g, b;
		if (h < 60) {
			(r, g, b) = (c, x, 0);
		} else if (h < 120) {
			(r, g, b) = (x, c, 0);
		} else if (h < 180) {
			(r, g, b) = (0, c, x);
		} else if (h < 240) {
			(r, g, b) = (0, x, c);
		} else if (h < 300) {
			(r, g, b) = (x, 0, c);
		} else {
			(r, g, b) = (c, 0, x);
		}

		return (To16(r + m), To16(g + m), To16(b + m));
	}

	private static ushort To16(double v) {
		var i = (int)Math.Round(v * 65535.0);
		return (ushort)Math.Min(65535, Math.Max(0, i));
	}
}
