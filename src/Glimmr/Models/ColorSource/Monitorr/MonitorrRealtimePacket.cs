using System;
using System.Buffers.Binary;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Glimmr.Models.ColorSource.Monitorr;

public sealed class MonitorrRealtimePacket {
	private static readonly byte[] Magic = Encoding.ASCII.GetBytes("MRTL");
	private const int HeaderLength = 32;
	private const byte CurrentVersion = 1;

	public uint Sequence { get; private init; }
	public long TimestampMs { get; private init; }
	public int LeftCount { get; private init; }
	public int RightCount { get; private init; }
	public int TopCount { get; private init; }
	public int BottomCount { get; private init; }
	public uint LayoutHash { get; private init; }
	public Color[] Colors { get; private init; } = Array.Empty<Color>();

	public int LedCount => LeftCount + RightCount + TopCount + BottomCount;
	public int[] Dimensions => new[] { LeftCount, RightCount, TopCount, BottomCount };

	public static MonitorrRealtimePacket Decode(ReadOnlySpan<byte> input) {
		if (input.Length < HeaderLength) {
			throw new ArgumentOutOfRangeException(nameof(input), "Monitorr realtime packet is shorter than the header.");
		}

		if (!input[..Magic.Length].SequenceEqual(Magic)) {
			throw new ArgumentException("Invalid Monitorr realtime packet magic.", nameof(input));
		}

		var version = input[4];
		if (version != CurrentVersion) {
			throw new NotSupportedException($"Unsupported Monitorr realtime version {version}.");
		}

		var leftCount = BinaryPrimitives.ReadUInt16LittleEndian(input[20..22]);
		var rightCount = BinaryPrimitives.ReadUInt16LittleEndian(input[22..24]);
		var topCount = BinaryPrimitives.ReadUInt16LittleEndian(input[24..26]);
		var bottomCount = BinaryPrimitives.ReadUInt16LittleEndian(input[26..28]);
		var ledCount = leftCount + rightCount + topCount + bottomCount;
		var expectedLength = HeaderLength + ledCount * 3;
		if (input.Length != expectedLength) {
			throw new ArgumentOutOfRangeException(nameof(input),
				$"Monitorr realtime packet has {input.Length} bytes; expected {expectedLength}.");
		}

		var colors = new Color[ledCount];
		var offset = HeaderLength;
		for (var i = 0; i < ledCount; i++) {
			colors[i] = Color.FromArgb(input[offset], input[offset + 1], input[offset + 2]);
			offset += 3;
		}

		return new MonitorrRealtimePacket {
			Sequence = BinaryPrimitives.ReadUInt32LittleEndian(input[8..12]),
			TimestampMs = BinaryPrimitives.ReadInt64LittleEndian(input[12..20]),
			LeftCount = leftCount,
			RightCount = rightCount,
			TopCount = topCount,
			BottomCount = bottomCount,
			LayoutHash = BinaryPrimitives.ReadUInt32LittleEndian(input[28..32]),
			Colors = colors
		};
	}
}
