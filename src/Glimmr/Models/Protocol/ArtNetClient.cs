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
/// Art-Net 4 client. Sends OpDmx packets over UDP port 6454.
/// Spec: https://art-net.org.uk/
/// </summary>
public sealed class ArtNetClient : IDisposable {
	private const int DefaultPort = 6454;
	private const int MaxChannelsPerUniverse = 512;
	private const int MaxRgbPixelsPerUniverse = 170; // 170 * 3 = 510 channels
	private const int HeaderSize = 18;
	private const ushort ProtocolVersion = 14;
	private static readonly byte[] ArtNetId = Encoding.ASCII.GetBytes("Art-Net\0");

	private readonly UdpClient _udp;
	private byte _sequence;
	private bool _disposed;

	public ArtNetClient() {
		_udp = new UdpClient { DontFragment = true };
		_udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
	}

	/// <param name="colors">Pixel colors to send.</param>
	/// <param name="ipAddress">Target device IP address.</param>
	/// <param name="startUniverse">First Art-Net universe (0-based).</param>
	public async Task SendFrame(Color[] colors, string ipAddress, int startUniverse = 0) {
		if (_disposed) throw new ObjectDisposedException(nameof(ArtNetClient));
		if (colors.Length == 0) return;

		var endpoint = new IPEndPoint(IPAddress.Parse(ipAddress), DefaultPort);
		var totalPixels = colors.Length;
		var pixelsSent = 0;
		var universe = startUniverse;

		_sequence = (byte)((_sequence % 255) + 1);

		while (pixelsSent < totalPixels) {
			var pixelsInUniverse = Math.Min(MaxRgbPixelsPerUniverse, totalPixels - pixelsSent);
			var packet = BuildOpDmx(colors, pixelsSent, pixelsInUniverse, universe);
			await _udp.SendAsync(packet, packet.Length, endpoint).ConfigureAwait(false);

			pixelsSent += pixelsInUniverse;
			universe++;
		}
	}

	private byte[] BuildOpDmx(Color[] colors, int offset, int pixelCount, int universe) {
		var channelCount = pixelCount * 3;
		// DMX data length must be even per Art-Net spec
		var dmxLength = channelCount % 2 == 0 ? channelCount : channelCount + 1;
		var packet = new byte[HeaderSize + dmxLength];

		// "Art-Net\0" identifier
		Buffer.BlockCopy(ArtNetId, 0, packet, 0, 8);

		// OpCode 0x5000 (OpDmx) — little-endian
		packet[8] = 0x00;
		packet[9] = 0x50;

		// Protocol version — big-endian
		packet[10] = (byte)(ProtocolVersion >> 8);
		packet[11] = (byte)(ProtocolVersion & 0xFF);

		packet[12] = _sequence;
		packet[13] = 0; // Physical port

		// Universe — little-endian
		packet[14] = (byte)(universe & 0xFF);
		packet[15] = (byte)((universe >> 8) & 0x7F);

		// Data length — big-endian
		BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(16), (ushort)dmxLength);

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
