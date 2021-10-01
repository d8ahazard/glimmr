using System;
using System.Drawing;
using System.Linq;

namespace Glimmr.Models.ColorSource.UDP {
	public class ColorPacket {
		
		public Color[] Colors { get; private set; }

		private UdpStreamMode UdpStreamMode { get; set; }
		
		public int Duration { get; private set; }
		
		public ColorPacket(Color[] colors, UdpStreamMode mode = UdpStreamMode.Drgb) {
			UdpStreamMode = mode;
			Colors = colors;
		}

		public ColorPacket(byte[] bytes) {
			Colors = Array.Empty<Color>();
			DecodePacket(bytes);
		}

		public byte[] Encode(int duration = 1) {
			var header = new[] {(byte)UdpStreamMode, (byte)duration};
			var data = Array.Empty<byte>();
			Duration = duration;
			data = UdpStreamMode switch {
				UdpStreamMode.Drgb => EncodeDrgb(),
				UdpStreamMode.Dnrgb => EncodeDnrgb(),
				UdpStreamMode.Drgbw => EncodeDrgbw(),
				UdpStreamMode.Warls => EncodeWarls(),
				_ => data
			};

			return header.Concat(data).ToArray();
		}

		private byte[] EncodeWarls() {
			var output = new byte[Colors.Length * 4];
			for (var i = 0; i < Colors.Length; i++) {
				output[i] = (byte)i;
				output[i + 1] = Colors[i].R;
				output[i + 2] = Colors[i].G;
				output[i + 3] = Colors[i].B;
			}
			return output;
		}

		private byte[] EncodeDrgbw() {
			var output = new byte[Colors.Length * 4];
			var dIdx = 0;
			foreach (var color in Colors) {
				output[dIdx] = color.R;
				output[dIdx+1] = color.G;
				output[dIdx+2] = color.B;
				output[dIdx+3] = color.A;
				dIdx += 4;
			}

			return output;
		}

		private byte[] EncodeDnrgb() {
			var data = new byte[Colors.Length * 3 + 2];
			var len = BitConverter.GetBytes((short) Colors.Length);
			data[0] = len[0];
			data[1] = len[1];
			var dIdx = 2;
			foreach (var color in Colors) {
				data[dIdx] = color.R;
				data[dIdx + 1] = color.G;
				data[dIdx + 2] = color.B;
				dIdx += 3;
			}
			return data;
		}

		private byte[] EncodeDrgb() {
			var output = new byte[Colors.Length * 3];
			var oIdx = 0;
			foreach (var t in Colors) {
				output[oIdx] = t.R;
				output[oIdx + 1] = t.G;
				output[oIdx + 2] = t.B;
				oIdx += 3;
			}
			return output;
		}

		private void DecodePacket(byte[] input) {
			if (input.Length < 2) {
				throw new ArgumentOutOfRangeException(nameof(input));
			}
			UdpStreamMode = (UdpStreamMode) input[0];
			Duration = input[1];
			switch (UdpStreamMode) {
				case UdpStreamMode.Drgb:
					DecodeDrgb(input.Skip(2).ToArray());
					break;
				case UdpStreamMode.Dnrgb:
					DecodeDnrgb(input.Skip(2).ToArray());
					break;
				case UdpStreamMode.Drgbw:
					DecodeDrgbw(input.Skip(2).ToArray());
					break;
				case UdpStreamMode.Warls:
					DecodeWarls(input.Skip(2).ToArray());
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(UdpStreamMode));
			}
		}

		private void DecodeWarls(byte[] input) {
			if (input.Length % 4 != 0) throw new ArgumentOutOfRangeException(nameof(input));
			var index = 0;
			for (var i = 0; i < input.Length; i += 4) {
				index = Math.Max(input[i], index);
			}

			if (Colors.Length < index + 1) {
				var col = Colors;
				Array.Resize(ref col,index + 1);
				Colors = col;
			}

			for (var i = 0; i < input.Length; i += 4) {
				Colors[input[i]] = Color.FromArgb(input[i + 1], input[i + 2], input[i + 3]);
			}
		}

		private void DecodeDrgbw(byte[] input) {
			if (input.Length % 4 != 0) throw new ArgumentOutOfRangeException(nameof(input));
			Colors = new Color[input.Length / 4];
			var cIdx = 0;
			for (var i = 0; i < input.Length; i += 4) {
				Colors[cIdx] = Color.FromArgb(input[i+3],input[i], input[i + 1], input[i + 2]);
				cIdx++;
			}
		}

		private void DecodeDnrgb(byte[] toArray) {
			var hi = toArray[0];
			var lo = toArray[1];
			var input = toArray.Skip(2).ToArray();
			if (input.Length % 3 != 0) throw new ArgumentOutOfRangeException(nameof(toArray));
			var start = BitConverter.ToInt16(new[] {hi, lo});
			var len = start + input.Length / 3;
			var cols = Colors;
			if (cols.Length > len) {
				Array.Resize(ref cols, len);
				Colors = cols;
			}
			var cIdx = start;
			for (var i = 0; i < input.Length; i += 3) {
				Colors[cIdx] = Color.FromArgb(input[i], input[i + 1], input[i + 2]);
				cIdx++;
			}
		}

		private void DecodeDrgb(byte[] input) {
			if (input.Length % 3 != 0) throw new ArgumentOutOfRangeException(nameof(input));
			Colors = new Color[input.Length / 3];
			var cIdx = 0;
			for (var i = 0; i < input.Length; i += 3) {
				Colors[cIdx] = Color.FromArgb(input[i], input[i + 1], input[i + 2]);
				cIdx++;
			}
		}
	}

	public enum UdpStreamMode {
		Warls= 1,
		Drgb= 2,
		Drgbw= 3,
		Dnrgb=4
	}
}