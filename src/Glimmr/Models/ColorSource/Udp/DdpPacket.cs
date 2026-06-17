#region

using System.Drawing;

#endregion

namespace Glimmr.Models.ColorSource.Udp;

/// <summary>
///     Minimal DDP (Distributed Display Protocol) encoder for streaming pixel data to WLED.
///     WLED listens for DDP on UDP port 4048 and reassembles a frame by byte offset, only
///     latching it when the PUSH flag is set on the final packet - so unlike hand-chunked
///     DNRGB there is no fragile packet boundary that can drop pixels.
/// </summary>
public static class DdpPacket {
	/// <summary>WLED's DDP listen port.</summary>
	public const int Port = 4048;

	/// <summary>Max RGB pixels per packet (1440 data bytes, the DDP-recommended ethernet size).</summary>
	public const int MaxPixelsPerPacket = 480;

	private const int HeaderLen = 10;
	private const byte Version1 = 0x40; // VV = 01 (DDP_FLAGS_VER1)
	private const byte PushFlag = 0x01; // DDP_FLAGS_PUSH
	private const byte TypeRgb24 = 0x0B; // RGB, 8 bits/element (WLED DDP_TYPE_RGB24)

	/// <summary>
	///     RGBW, 8 bits/element (WLED <c>DDP_TYPE_RGBW32</c>). Not used by the active RGB path;
	///     a future RGBW matrix path would select this and pack 4 bytes/pixel.
	/// </summary>
	public const byte TypeRgbw32 = 0x1B;

	private const byte IdDisplay = 0x01; // default output device

	/// <summary>
	///     Encode a single DDP write packet for <paramref name="count" /> pixels starting at
	///     <paramref name="startPixel" />. Set <paramref name="push" /> on the final packet of a
	///     frame so WLED displays the whole buffer at once.
	/// </summary>
	public static byte[] Encode(Color[] colors, int startPixel, int count, bool push, int sequence = 0) {
		var packet = new byte[HeaderLen + count * 3];
		EncodeInto(packet, colors, startPixel, count, push, sequence);
		return packet;
	}

	/// <summary>
	///     Encode a DDP write packet into a caller-provided buffer (which must be at least
	///     <c>10 + count*3</c> bytes) and return the packet length. Lets the caller reuse buffers
	///     across frames to avoid per-frame allocations on the hot path.
	///     <paramref name="sequence" /> is written to the low nibble of the sequence byte (DDP uses
	///     1-15; pass 0 to leave it unsequenced).
	/// </summary>
	public static int EncodeInto(byte[] packet, Color[] colors, int startPixel, int count, bool push,
		int sequence = 0) {
		var byteOffset = startPixel * 3;
		var dataLen = count * 3;

		packet[0] = (byte)(Version1 | (push ? PushFlag : 0));
		packet[1] = (byte)(sequence & 0x0F);
		packet[2] = TypeRgb24;
		packet[3] = IdDisplay;
		packet[4] = (byte)((byteOffset >> 24) & 0xFF);
		packet[5] = (byte)((byteOffset >> 16) & 0xFF);
		packet[6] = (byte)((byteOffset >> 8) & 0xFF);
		packet[7] = (byte)(byteOffset & 0xFF);
		packet[8] = (byte)((dataLen >> 8) & 0xFF);
		packet[9] = (byte)(dataLen & 0xFF);

		var j = HeaderLen;
		var end = startPixel + count;
		for (var i = startPixel; i < end; i++) {
			var c = colors[i];
			packet[j++] = c.R;
			packet[j++] = c.G;
			packet[j++] = c.B;
		}

		return HeaderLen + dataLen;
	}

	/// <summary>Max DDP packet size in bytes (header + max data).</summary>
	public const int MaxPacketBytes = HeaderLen + MaxPixelsPerPacket * 3;
}
