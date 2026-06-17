#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.ColorSource.Udp;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Wled;

/// <summary>
///     Central sender-side frame coordinator for WLED matrix targets streaming over DDP.
///     Each matrix <see cref="WledDevice" /> registers its socket and submits the latest mapped
///     frame; this owns the single send loop that keeps every device in lock-step:
///     <list type="number">
///         <item>Phase 1 - send every device's non-PUSH packets and wait for them to drain.</item>
///         <item>Phase 2 - release every device's final PUSH packet back-to-back so all panels
///         latch their new frame at (very nearly) the same instant.</item>
///     </list>
///     Submissions are latest-frame-wins: if the render loop outruns the network, the older
///     pending frame is dropped rather than queued, which keeps inter-device skew and jitter low.
/// </summary>
public sealed class WledMatrixStreamer {
	// Pure stall failsafe: if a registered panel stops submitting entirely (misconfigured/dead
	// content path) the barrier would never complete, so the loop wakes after this long and
	// sends whatever HAS staged. Panels driven by the same color-send event submit within
	// sub-millisecond of each other, so this is deliberately MUCH larger than a frame interval -
	// it must not fire at the per-frame boundary (that would split a pair), only on a real stall.
	// Kept modest so a genuinely stalled panel doesn't freeze the healthy one for long.
	private const int BarrierTimeoutMs = 100;

	private readonly ConcurrentDictionary<string, MatrixChannel> _channels = new();
	private readonly object _lifecycle = new();

	// Released only when EVERY registered panel has a frame staged, so both matrices are always
	// sent the same content in one synchronized pass (this is the cross-device frame barrier).
	private readonly ManualResetEventSlim _frameReady = new(false);
	private readonly Stopwatch _diagWatch = Stopwatch.StartNew();

	private CancellationTokenSource? _cts;
	private Task? _loop;
	private long _frameId;
	private long _droppedFrames;
	private long _partialFrames;

	/// <summary>
	///     Register a matrix target. The streamer borrows <paramref name="socket" /> for sending
	///     but never disposes it - the owning device controls its lifetime.
	/// </summary>
	public void Register(string id, IPEndPoint endpoint, UdpClient socket, int ledCount) {
		if (string.IsNullOrEmpty(id) || ledCount <= 0) {
			return;
		}

		_channels[id] = new MatrixChannel(id, endpoint, socket, ledCount);

		lock (_lifecycle) {
			if (_loop is { IsCompleted: false }) {
				return;
			}

			_cts = new CancellationTokenSource();
			_loop = Task.Run(() => SendLoop(_cts.Token));
		}
	}

	public void Unregister(string id) {
		_channels.TryRemove(id, out _);
		// The remaining panels may now satisfy the barrier (the departed one was being waited on).
		if (AllChannelsReady()) {
			_frameReady.Set();
		}

		lock (_lifecycle) {
			if (!_channels.IsEmpty || _cts == null) {
				return;
			}

			_cts.Cancel();
			_cts.Dispose();
			_cts = null;
			_loop = null;
		}
	}

	/// <summary>
	///     Deposit the latest mapped wire frame for a device. Cheap (one copy) and non-blocking so
	///     it never gates the color pipeline; overwriting an unsent frame is the drop policy. Once
	///     every registered panel has a pending frame the barrier is released so the loop sends
	///     them together.
	/// </summary>
	public void Submit(string id, Color[] wire) {
		if (!_channels.TryGetValue(id, out var channel)) {
			return;
		}

		if (channel.Submit(wire)) {
			Interlocked.Increment(ref _droppedFrames);
		}

		if (AllChannelsReady()) {
			_frameReady.Set();
		}
	}

	/// <summary>True if at least one panel is registered and every registered panel has a frame
	/// staged and ready to send.</summary>
	private bool AllChannelsReady() {
		var any = false;
		foreach (var channel in _channels.Values) {
			any = true;
			if (!channel.HasNew) {
				return false;
			}
		}

		return any;
	}

	private async Task SendLoop(CancellationToken ct) {
		var staged = new List<MatrixChannel>();
		var nonPush = new List<Task>();
		var push = new List<Task>();
		var encodeWatch = new Stopwatch();

		while (!ct.IsCancellationRequested) {
			bool barrierMet;
			try {
				// Block until every registered panel has staged a frame (barrierMet == true), or
				// fall back after the timeout so a stalled panel can't freeze the others.
				barrierMet = _frameReady.Wait(BarrierTimeoutMs, ct);
			} catch (OperationCanceledException) {
				break;
			}

			// Reset before staging: any submit that completes a NEW batch after this point will
			// re-set the event, so we never lose a wake-up for the frame we're about to consume.
			_frameReady.Reset();

			staged.Clear();
			var registered = 0;
			foreach (var channel in _channels.Values) {
				registered++;
				if (channel.Stage()) {
					staged.Add(channel);
				}
			}

			if (staged.Count == 0) {
				continue;
			}

			// Sent without the full set of panels staged - track it so skew is visible in diags.
			if (!barrierMet && staged.Count < registered) {
				Interlocked.Increment(ref _partialFrames);
			}

			var frameId = Interlocked.Increment(ref _frameId);
			encodeWatch.Restart();
			foreach (var channel in staged) {
				channel.Encode();
			}

			var encodeMs = encodeWatch.Elapsed.TotalMilliseconds;

			// Phase 1: stage all non-PUSH packets across every device.
			nonPush.Clear();
			foreach (var channel in staged) {
				channel.QueueNonPush(nonPush);
			}

			if (nonPush.Count > 0) {
				await Task.WhenAll(nonPush).ConfigureAwait(false);
			}

			// Phase 2: release the final PUSH packets back-to-back so panels latch together.
			push.Clear();
			foreach (var channel in staged) {
				push.Add(channel.SendPush());
			}

			await Task.WhenAll(push).ConfigureAwait(false);

			if (_diagWatch.ElapsedMilliseconds > 5000) {
				_diagWatch.Restart();
				Log.Debug(
					$"WledMatrixStreamer: frame {frameId}, {staged.Count} device(s) latched together, " +
					$"encode {encodeMs:F2}ms, dropped {Interlocked.Read(ref _droppedFrames)}, " +
					$"partial {Interlocked.Read(ref _partialFrames)} since start.");
			}
		}
	}

	/// <summary>Per-device send state: socket, latest-frame mailbox, and reusable DDP buffers.</summary>
	private sealed class MatrixChannel {
		private readonly IPEndPoint _endpoint;
		private readonly int _ledCount;
		private readonly object _mailbox = new();
		private readonly byte[][] _packets;
		private readonly int[] _packetLens;
		private readonly Color[] _snapshot;
		private readonly Color[] _stage;
		private readonly UdpClient _socket;
		private readonly string _tag;
		private readonly Stopwatch _udpWatch = Stopwatch.StartNew();

		private volatile bool _hasNew;
		private int _packetCount;
		private int _seq;

		/// <summary>Whether a frame is staged and waiting to be sent. Read by the barrier check.</summary>
		public bool HasNew => _hasNew;

		public MatrixChannel(string id, IPEndPoint endpoint, UdpClient socket, int ledCount) {
			_tag = id;
			_endpoint = endpoint;
			_socket = socket;
			_ledCount = ledCount;
			_snapshot = new Color[ledCount];
			_stage = new Color[ledCount];
			var packetCount = (ledCount + DdpPacket.MaxPixelsPerPacket - 1) / DdpPacket.MaxPixelsPerPacket;
			_packets = new byte[packetCount][];
			_packetLens = new int[packetCount];
			for (var i = 0; i < packetCount; i++) {
				_packets[i] = new byte[DdpPacket.MaxPacketBytes];
			}
		}

		/// <summary>Copy the newest wire frame into the mailbox. Returns true if it overwrote an
		/// unsent frame (a dropped frame).</summary>
		public bool Submit(Color[] wire) {
			lock (_mailbox) {
				var dropped = _hasNew;
				var count = Math.Min(wire.Length, _ledCount);
				Array.Copy(wire, _snapshot, count);
				_hasNew = true;
				return dropped;
			}
		}

		/// <summary>Promote the mailbox frame to the loop-owned stage buffer. Returns false if no
		/// new frame is pending.</summary>
		public bool Stage() {
			lock (_mailbox) {
				if (!_hasNew) {
					return false;
				}

				Array.Copy(_snapshot, _stage, _ledCount);
				_hasNew = false;
				return true;
			}
		}

		public void Encode() {
			var idx = 0;
			for (var start = 0; start < _ledCount; start += DdpPacket.MaxPixelsPerPacket) {
				var count = Math.Min(DdpPacket.MaxPixelsPerPacket, _ledCount - start);
				var push = start + count >= _ledCount;
				_seq = _seq % 15 + 1;
				_packetLens[idx] = DdpPacket.EncodeInto(_packets[idx], _stage, start, count, push, _seq);
				idx++;
			}

			_packetCount = idx;
		}

		public void QueueNonPush(List<Task> sink) {
			for (var i = 0; i < _packetCount - 1; i++) {
				sink.Add(Send(_packets[i], _packetLens[i]));
			}
		}

		public Task SendPush() {
			var last = _packetCount - 1;
			return last < 0 ? Task.CompletedTask : Send(_packets[last], _packetLens[last]);
		}

		private async Task Send(byte[] packet, int len) {
			try {
				await _socket.SendAsync(packet, len, _endpoint).ConfigureAwait(false);
			} catch (Exception e) {
				if (_udpWatch.ElapsedMilliseconds > 5000) {
					_udpWatch.Restart();
					Log.Warning($"{_tag}::DDP send to {_endpoint} failed: {e.Message}");
				}
			}
		}
	}
}
