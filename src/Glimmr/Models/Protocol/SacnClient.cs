#region

using System;
using System.Buffers.Binary;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

#endregion

namespace Glimmr.Models.Protocol;

/// <summary>
/// sACN / ANSI E1.31 client. Sends DMX data via UDP port 5568.
/// Supports unicast; multicast address is 239.255.{universe_hi}.{universe_lo}.
/// </summary>
public sealed class SacnClient : IDisposable {
	private const int DefaultPort = 5568;
	private const int MaxChannelsPerUniverse = 512;
	private const int MaxRgbPixelsPerUniverse = 170; // 170 * 3 = 510 channels
	private const byte StartCode = 0x00;
	private const int RootLayerOffset = 0;
	private const int FramingLayerOffset = 38;
	private const int DmpLayerOffset = 115;
	private const int DmxDataOffset = 126;

	private static readonly byte[] AcnPacketIdentifier = {
		0x41, 0x53, 0x43, 0x2D, 0x45, 0x31, 0x2E, 0x31,
		0x37, 0x00, 0x00, 0x00
	};

	private readonly UdpClient _udp;
	private readonly Guid _cid;
	private readonly string _sourceName;
	private byte _sequence;
	private bool _disposed;

	public SacnClient(string? sourceName = null) {
		_udp = new UdpClient { DontFragment = true };
		_udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
		_cid = Guid.NewGuid();
		_sourceName = sourceName ?? "Glimmr";
	}

	/// <param name="colors">Pixel colors to send.</param>
	/// <param name="ipAddress">Target device IP address (unicast). Pass null or empty for multicast.</param>
	/// <param name="startUniverse">First DMX universe (1-based per E1.31 convention).</param>
	public async Task SendFrame(Color[] colors, string ipAddress, int startUniverse = 1) {
		if (_disposed) throw new ObjectDisposedException(nameof(SacnClient));
		if (colors.Length == 0) return;

		var totalPixels = colors.Length;
		var pixelsSent = 0;
		var universe = startUniverse;

		_sequence = (byte)((_sequence + 1) % 256);

		while (pixelsSent < totalPixels) {
			var pixelsInUniverse = Math.Min(MaxRgbPixelsPerUniverse, totalPixels - pixelsSent);
			var channelCount = pixelsInUniverse * 3;

			var endpoint = ResolveEndpoint(ipAddress, universe);
			var packet = BuildPacket(colors, pixelsSent, pixelsInUniverse, channelCount, universe);
			await _udp.SendAsync(packet, packet.Length, endpoint).ConfigureAwait(false);

			pixelsSent += pixelsInUniverse;
			universe++;
		}
	}

	private static IPEndPoint ResolveEndpoint(string ipAddress, int universe) {
		if (!string.IsNullOrEmpty(ipAddress)) {
			return new IPEndPoint(IPAddress.Parse(ipAddress), DefaultPort);
		}

		var hi = (byte)((universe >> 8) & 0xFF);
		var lo = (byte)(universe & 0xFF);
		return new IPEndPoint(IPAddress.Parse($"239.255.{hi}.{lo}"), DefaultPort);
	}

	private byte[] BuildPacket(Color[] colors, int offset, int pixelCount, int channelCount, int universe) {
		// Total packet: 126 header bytes + 1 start code + channelCount DMX slots
		var dmxSlotCount = channelCount + 1; // +1 for start code
		var packetLength = DmxDataOffset + dmxSlotCount;
		var packet = new byte[packetLength];

		WriteRootLayer(packet, packetLength);
		WriteFramingLayer(packet, packetLength, universe);
		WriteDmpLayer(packet, dmxSlotCount);

		// DMX start code
		packet[DmxDataOffset] = StartCode;

		// RGB channel data
		var pos = DmxDataOffset + 1;
		for (var i = 0; i < pixelCount; i++) {
			var c = colors[offset + i];
			packet[pos++] = c.R;
			packet[pos++] = c.G;
			packet[pos++] = c.B;
		}

		return packet;
	}

	private void WriteRootLayer(byte[] packet, int packetLength) {
		// Preamble size (0x0010)
		BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(0), 0x0010);
		// Post-amble size (0x0000)
		BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2), 0x0000);
		// ACN Packet Identifier
		Buffer.BlockCopy(AcnPacketIdentifier, 0, packet, 4, 12);

		// Flags + Length for root layer: covers from byte 16 to end
		var rootPduLength = packetLength - 16;
		var flagsAndLength = (ushort)(0x7000 | (rootPduLength & 0x0FFF));
		BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(16), flagsAndLength);

		// Root vector: VECTOR_ROOT_E131_DATA = 0x00000004
		BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(18), 0x00000004);

		// CID (16 bytes)
		var cidBytes = _cid.ToByteArray();
		Buffer.BlockCopy(cidBytes, 0, packet, 22, 16);
	}

	private void WriteFramingLayer(byte[] packet, int packetLength, int universe) {
		// Flags + Length for framing layer: covers from byte 40 to end
		var framingPduLength = packetLength - 38;
		var flagsAndLength = (ushort)(0x7000 | (framingPduLength & 0x0FFF));
		BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(38), flagsAndLength);

		// Framing vector: VECTOR_E131_DATA_PACKET = 0x00000002
		BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(40), 0x00000002);

		// Source name (64 bytes, UTF-8, null-padded)
		var nameBytes = Encoding.UTF8.GetBytes(_sourceName);
		var copyLen = Math.Min(nameBytes.Length, 63);
		Buffer.BlockCopy(nameBytes, 0, packet, 44, copyLen);

		// Priority (default 100)
		packet[108] = 100;

		// Synchronization address (0 = none)
		BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(109), 0);

		// Sequence number
		packet[111] = _sequence;

		// Options flags (0 = none)
		packet[112] = 0;

		// Universe
		BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(113), (ushort)universe);
	}

	private static void WriteDmpLayer(byte[] packet, int dmxSlotCount) {
		// Flags + Length for DMP layer: covers from byte 117 to end
		var dmpPduLength = dmxSlotCount + 11; // 11 = DMP header fields
		var flagsAndLength = (ushort)(0x7000 | (dmpPduLength & 0x0FFF));
		BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(115), flagsAndLength);

		// DMP vector: VECTOR_DMP_SET_PROPERTY = 0x02
		packet[117] = 0x02;

		// Address & Data Type: 0xA1 (identifies format)
		packet[118] = 0xA1;

		// First property address (0x0000)
		BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(119), 0x0000);

		// Address increment (0x0001)
		BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(121), 0x0001);

		// Property value count (channels + start code)
		BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(123), (ushort)dmxSlotCount);

		// Start code at packet[125] handled by caller
	}

	public void Dispose() {
		if (_disposed) return;
		_disposed = true;
		try { _udp.Close(); } catch { /* best-effort */ }
		_udp.Dispose();
	}
}
