extern alias bc2;

#region

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using bc2::Org.BouncyCastle.Security;
using bc2::Org.BouncyCastle.Tls;
using bc2::Org.BouncyCastle.Tls.Crypto.Impl.BC;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.HueSync;

/// <summary>
///     Minimal DTLS 1.2 PSK server that terminates the Hue entertainment stream on UDP 2100.
///     Uses BouncyCastle (pure-managed) so it runs identically in Docker, on a Pi, and bare metal.
///     Decoded frames are published to <see cref="HueSyncBridge" />.
/// </summary>
public sealed class HueDtlsServer {
	public const int EntertainmentPort = 2100;

	private readonly HueSyncBridge _bridge;
	private CancellationTokenSource? _cts;
	private Task? _loop;

	public HueDtlsServer(HueSyncBridge bridge) {
		_bridge = bridge;
	}

	public void Start() {
		if (_loop is { IsCompleted: false }) {
			return;
		}

		_cts = new CancellationTokenSource();
		var token = _cts.Token;
		_loop = Task.Run(() => RunLoop(token), token);
		Log.Information($"HueSync: DTLS entertainment server listening on UDP {EntertainmentPort}.");
	}

	public void Stop() {
		try {
			_cts?.Cancel();
		} catch (Exception) {
			// ignored
		}

		_bridge.SetStreaming(false);
	}

	private void RunLoop(CancellationToken token) {
		while (!token.IsCancellationRequested) {
			try {
				AcceptSession(token);
			} catch (OperationCanceledException) {
				break;
			} catch (Exception e) {
				Log.Debug("HueSync: DTLS session ended: " + e.Message);
			} finally {
				_bridge.SetStreaming(false);
			}

			if (!token.IsCancellationRequested) {
				Thread.Sleep(250);
			}
		}
	}

	private void AcceptSession(CancellationToken token) {
		using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
		socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
		socket.Bind(new IPEndPoint(IPAddress.Any, EntertainmentPort));
		socket.ReceiveTimeout = 1000;

		// Peek the first datagram to learn the remote endpoint, then connect so the BouncyCastle
		// transport can do plain Send/Receive. The peeked datagram stays queued for the handshake.
		var probe = new byte[2048];
		EndPoint remote = new IPEndPoint(IPAddress.Any, 0);
		while (!token.IsCancellationRequested) {
			try {
				socket.ReceiveFrom(probe, 0, probe.Length, SocketFlags.Peek, ref remote);
				break;
			} catch (SocketException se) when (se.SocketErrorCode == SocketError.TimedOut) {
				// no client yet; keep waiting
			} catch (SocketException se) when (se.SocketErrorCode == SocketError.MessageSize) {
				// datagram larger than probe buffer; that's fine, we still learned the remote
				break;
			}
		}

		if (token.IsCancellationRequested) {
			return;
		}

		Log.Information($"HueSync: Incoming entertainment connection from {remote}.");
		socket.Connect(remote);

		var crypto = new BcTlsCrypto(new SecureRandom());
		var server = new HuePskTlsServer(crypto, _bridge);
		var protocol = new DtlsServerProtocol();
		var transport = new SocketDatagramTransport(socket);

		var dtls = protocol.Accept(server, transport);
		Log.Information("HueSync: DTLS handshake complete; receiving entertainment frames.");
		_bridge.SetStreaming(true);

		var buffer = new byte[dtls.GetReceiveLimit()];
		while (!token.IsCancellationRequested) {
			int received;
			try {
				received = dtls.Receive(buffer, 0, buffer.Length, 1000);
			} catch (SocketException se) when (se.SocketErrorCode == SocketError.TimedOut) {
				continue;
			}

			if (received < 0) {
				continue;
			}

			if (received == 0) {
				break;
			}

			var frame = new byte[received];
			Array.Copy(buffer, frame, received);
			var colors = EntertainmentFrameParser.Parse(frame);
			if (colors is { Length: > 0 }) {
				_bridge.PublishFrame(colors);
			}
		}

		try {
			dtls.Close();
		} catch (Exception) {
			// ignored
		}
	}
}
