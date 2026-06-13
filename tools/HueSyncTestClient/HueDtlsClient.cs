using System.Net.Sockets;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;

namespace HueSyncTestClient;

/// <summary>
///     Minimal DTLS 1.2 PSK client (mirrors the Hue Sync app) used to stream entertainment frames
///     to the emulated bridge for testing.
/// </summary>
public static class HueDtlsClient {
	public static DtlsTransport Connect(Socket connectedSocket, byte[] identity, byte[] psk) {
		var crypto = new BcTlsCrypto(new SecureRandom());
		var client = new PskClient(crypto, identity, psk);
		var protocol = new DtlsClientProtocol();
		var transport = new ClientDatagramTransport(connectedSocket);
		return protocol.Connect(client, transport);
	}

	private sealed class PskClient : PskTlsClient {
		public PskClient(TlsCrypto crypto, byte[] identity, byte[] psk)
			: base(crypto, new BasicTlsPskIdentity(identity, psk)) {
		}

		protected override ProtocolVersion[] GetSupportedVersions() {
			return ProtocolVersion.DTLSv12.Only();
		}

		protected override int[] GetSupportedCipherSuites() {
			return new[] { CipherSuite.TLS_PSK_WITH_AES_128_GCM_SHA256 };
		}
	}

	private sealed class ClientDatagramTransport : DatagramTransport {
		private const int Mtu = 1500;
		private readonly Socket _socket;

		public ClientDatagramTransport(Socket socket) {
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
			} catch (SocketException) {
				return -1;
			}
		}

		public int Receive(Span<byte> buffer, int waitMillis) {
			_socket.ReceiveTimeout = waitMillis <= 0 ? 1 : waitMillis;
			try {
				return _socket.Receive(buffer, SocketFlags.None);
			} catch (SocketException) {
				return -1;
			}
		}

		public void Send(byte[] buf, int off, int len) {
			try {
				_socket.Send(buf, off, len, SocketFlags.None);
			} catch (SocketException) {
				// ignored
			}
		}

		public void Send(ReadOnlySpan<byte> buffer) {
			try {
				_socket.Send(buffer, SocketFlags.None);
			} catch (SocketException) {
				// ignored
			}
		}

		public void Close() {
			// socket owned by caller
		}
	}
}
