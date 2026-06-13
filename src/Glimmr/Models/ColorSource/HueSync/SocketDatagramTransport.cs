extern alias bc2;

#region

using System;
using System.Net.Sockets;
using bc2::Org.BouncyCastle.Tls;

#endregion

namespace Glimmr.Models.ColorSource.HueSync;

/// <summary>
///     Adapts a connected UDP <see cref="Socket" /> to the BouncyCastle
///     <see cref="DatagramTransport" /> contract used by the DTLS server.
/// </summary>
internal sealed class SocketDatagramTransport : DatagramTransport {
	private const int Mtu = 1500;
	private readonly Socket _socket;

	public SocketDatagramTransport(Socket socket) {
		_socket = socket;
	}

	public int GetReceiveLimit() {
		return Mtu;
	}

	public int GetSendLimit() {
		return Mtu;
	}

	public int Receive(byte[] buf, int off, int len, int waitMillis) {
		_socket.ReceiveTimeout = waitMillis <= 0 ? 1 : waitMillis;
		try {
			return _socket.Receive(buf, off, len, SocketFlags.None);
		} catch (SocketException se) when (se.SocketErrorCode == SocketError.TimedOut) {
			return -1;
		} catch (SocketException) {
			return -1;
		}
	}

	public int Receive(Span<byte> buffer, int waitMillis) {
		_socket.ReceiveTimeout = waitMillis <= 0 ? 1 : waitMillis;
		try {
			return _socket.Receive(buffer, SocketFlags.None);
		} catch (SocketException se) when (se.SocketErrorCode == SocketError.TimedOut) {
			return -1;
		} catch (SocketException) {
			return -1;
		}
	}

	public void Send(byte[] buf, int off, int len) {
		try {
			_socket.Send(buf, off, len, SocketFlags.None);
		} catch (SocketException) {
			// ignored - DTLS will retransmit
		}
	}

	public void Send(ReadOnlySpan<byte> buffer) {
		try {
			_socket.Send(buffer, SocketFlags.None);
		} catch (SocketException) {
			// ignored - DTLS will retransmit
		}
	}

	public void Close() {
		// The owning server disposes the socket.
	}
}
