#region

using System;
using System.Buffers.Binary;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

#endregion

namespace Glimmr.Models.Protocol;

/// <summary>
/// Distributed Display Protocol client. Sends RGB pixel data over UDP port 4048.
/// Spec: http://www.3waylabs.com/ddp/
/// </summary>
public sealed class DdpClient : IDisposable {
	private const int DefaultPort = 4048;
	private const int MaxPixelsPerPacket = 480;
	private const int HeaderSize = 10;

	private const byte FlagVer1 = 0x40;
	private const byte FlagPush = 0x01;
	private const byte FlagReply = 0x40;
	private const byte DataTypeRgb24 = 0x0A;
	private const byte DataTypeRgbw32 = 0x1A;

	private readonly UdpClient _udp;
	private byte _sequence;
	private bool _disposed;

	public DdpClient() {
		_udp = new UdpClient { DontFragment = true };
		_udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
	}

	/// <param name="colors">Pixel colors to send.</param>
	/// <param name="ipAddress">Target device IP address.</param>
	/// <param name="startUniverse">Unused for DDP; kept for interface parity.</param>
	public async Task SendFrame(Color[] colors, string ipAddress, int startUniverse = 0) {
		if (_disposed) throw new ObjectDisposedException(nameof(DdpClient));
		if (colors.Length == 0) return;

		var endpoint = new IPEndPoint(IPAddress.Parse(ipAddress), DefaultPort);
		var totalPixels = colors.Length;
		var pixelsSent = 0;

		while (pixelsSent < totalPixels) {
			var pixelsInChunk = Math.Min(MaxPixelsPerPacket, totalPixels - pixelsSent);
			var isLastPacket = pixelsSent + pixelsInChunk >= totalPixels;

			var packet = BuildPacket(colors, pixelsSent, pixelsInChunk, isLastPacket);
			await _udp.SendAsync(packet, packet.Length, endpoint).ConfigureAwait(false);

			pixelsSent += pixelsInChunk;
		}
	}

	private byte[] BuildPacket(Color[] colors, int offset, int pixelCount, bool push) {
		var bytesPerPixel = 3;
		var dataLength = pixelCount * bytesPerPixel;
		var packet = new byte[HeaderSize + dataLength];

		byte flags = FlagVer1;
		if (push) flags |= FlagPush;

		_sequence = (byte)((_sequence % 15) + 1);

		packet[0] = flags;
		packet[1] = _sequence;
		packet[2] = DataTypeRgb24;
		packet[3] = 0x01; // device ID: default source

		var byteOffset = offset * bytesPerPixel;
		BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(4), (uint)byteOffset);

		BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(8), (ushort)dataLength);

		var pos = HeaderSize;
		for (var i = 0; i < pixelCount; i++) {
			var c = colors[offset + i];
			packet[pos++] = c.R;
			packet[pos++] = c.G;
			packet[pos++] = c.B;
		}

		return packet;
	}

	public void Dispose() {
		if (_disposed) return;
		_disposed = true;
		try { _udp.Close(); } catch { /* best-effort */ }
		_udp.Dispose();
	}
}
