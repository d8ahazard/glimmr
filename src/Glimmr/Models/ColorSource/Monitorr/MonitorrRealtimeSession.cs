using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Models.Data;
using Glimmr.Models.Frame;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;

namespace Glimmr.Models.ColorSource.Monitorr;

public sealed class MonitorrRealtimeSession : IDisposable {
	private const int ReceiveBufferSize = 8192;

	private readonly ColorService _colorService;
	private readonly ControlService _controlService;
	private readonly FrameSplitter _splitter;
	private FrameBuilder? _builder;
	private uint? _lastSequence;
	private uint _layoutHash;
	private bool _modeReady;

	public MonitorrRealtimeSession(ColorService colorService) {
		_colorService = colorService;
		_controlService = colorService.ControlService;
		_splitter = new FrameSplitter(colorService) {
			DoSend = true,
			SourceActive = true
		};
	}

	public async Task Run(WebSocket socket, CancellationToken cancellationToken) {
		var buffer = new byte[ReceiveBufferSize];
		while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested) {
			var message = await ReceiveMessage(socket, buffer, cancellationToken).ConfigureAwait(false);
			if (message == null) {
				break;
			}

			try {
				var packet = MonitorrRealtimePacket.Decode(message);
				await ProcessPacket(packet).ConfigureAwait(false);
			} catch (Exception e) {
				Log.Warning("Exception parsing Monitorr realtime packet: " + e.Message);
			}
		}
	}

	private async Task ProcessPacket(MonitorrRealtimePacket packet) {
		if (IsStale(packet.Sequence)) {
			return;
		}

		await EnsureUdpMode().ConfigureAwait(false);
		EnsureBuilder(packet);

		if (_builder == null) {
			Log.Warning("Monitorr realtime builder is null.");
			return;
		}

		var frame = _builder.Build(packet.Colors);
		if (frame == null) {
			return;
		}

		await _splitter.Update(frame).ConfigureAwait(false);
		frame.Dispose();
		_lastSequence = packet.Sequence;
	}

	private bool IsStale(uint sequence) {
		if (_lastSequence == null) {
			return false;
		}

		return sequence <= _lastSequence.Value && _lastSequence.Value - sequence < 10_000;
	}

	private async Task EnsureUdpMode() {
		if (_modeReady) {
			return;
		}

		var sd = DataUtil.GetSystemData();
		if (sd.StreamMode != StreamMode.Udp) {
			DataUtil.SetItem("StreamMode", StreamMode.Udp);
		}

		if (sd.DeviceMode != DeviceMode.Udp) {
			await _controlService.SetMode(DeviceMode.Udp).ConfigureAwait(false);
		}

		_modeReady = true;
	}

	private void EnsureBuilder(MonitorrRealtimePacket packet) {
		if (_builder != null && _layoutHash == packet.LayoutHash) {
			return;
		}

		_builder?.Dispose();
		_builder = new FrameBuilder(packet.Dimensions);
		_layoutHash = packet.LayoutHash;
	}

	private static async Task<byte[]?> ReceiveMessage(WebSocket socket, byte[] buffer, CancellationToken cancellationToken) {
		await using var message = new MemoryStream();

		while (true) {
			var result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
			if (result.MessageType == WebSocketMessageType.Close) {
				if (socket.State == WebSocketState.CloseReceived) {
					await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken)
						.ConfigureAwait(false);
				}

				return null;
			}

			if (result.MessageType != WebSocketMessageType.Binary) {
				return Array.Empty<byte>();
			}

			message.Write(buffer, 0, result.Count);
			if (result.EndOfMessage) {
				return message.ToArray();
			}
		}
	}

	public void Dispose() {
		_builder?.Dispose();
		_splitter.Dispose();
	}
}
