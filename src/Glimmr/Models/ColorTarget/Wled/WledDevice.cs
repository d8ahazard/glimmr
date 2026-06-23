#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Models.ColorSource.Udp;
using Glimmr.Models.Util;
using Glimmr.Services;
using Kevsoft.WLED;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Wled;

public class WledDevice : ColorTarget, IColorTarget, IDisposable {
	private string IpAddress { get; set; }
	private const int Port = 21324;
	private static readonly HttpClient HttpClient = new();
	private readonly UdpClient _udpClient;
	private readonly WLedClient _wLedClient;

	private int _brightness;
	private WledData _data;

	private bool _disposed;
	private IPEndPoint? _ep;
	private IPEndPoint? _ddpEp;
	private int _ledCount;
	private float _multiplier;
	private int _offset;
	private int _protocol = 2;
	private WledSegment[] _segments;
	private StripMode _stripMode;
	private int _targetSector;

	private int _matrixWidth;
	private int _matrixHeight;
	private MatrixMode _matrixMode = MatrixMode.Mirror;
	private MatrixSide _matrixSide = MatrixSide.Bottom;
	private bool _flipHorizontal;
	private bool _flipVertical;
	private int _extendDepth = 50;
	private int _extendFade = 40;
	private Color[] _panel = Array.Empty<Color>();
	private Color[] _matrixWire = Array.Empty<Color>();
	private int _wireW;
	private int _wireH;

	// Throttles DDP send-failure warnings so a bad link can't flood the log.
	private readonly Stopwatch _udpWatch = Stopwatch.StartNew();

	// Shared coordinator that owns the synchronized DDP send loop for all matrix targets.
	private readonly WledMatrixStreamer _streamer;
	private bool _matrixRegistered;

	// Periodically re-asserts lor=0 while streaming so a user touching the WLED UI (which sets
	// lor=1 and makes WLED ignore realtime data) can't silently kill the stream until restart.
	private Timer? _lorWatchdog;
	private const int LorWatchdogMs = 10000;

	public WledDevice(WledData wd, ColorService cs) : base(cs) {
		_segments = Array.Empty<WledSegment>();
		_streamer = cs.WledMatrixStreamer;
		cs.ControlService.RefreshSystemEvent += RefreshSystem;
		// Each WLED gets its own socket. A single shared, non-blocking socket hammered by every
		// device fire-and-forget can overflow its send buffer and silently drop a weaker link's
		// packets (that was why .27 never received DDP). DontFragment is left off so large
		// datagrams fragment instead of being dropped on a lower-MTU WiFi path.
		_udpClient = new UdpClient();
		_data = wd ?? throw new ArgumentException("Invalid WLED data.");
		Id = _data.Id;
		IpAddress = _data.IpAddress;
		_brightness = _data.Brightness;
		_multiplier = _data.LedMultiplier;
		_wLedClient = new WLedClient("http://" + IpAddress + "/");
		ReloadData();
		ColorService.ColorSendEventAsync += SetColors;
	}

	public bool Enable { get; set; }
	public bool Streaming { get; set; }
	public string Id { get; }

	IColorTargetData IColorTarget.Data {
		get => _data;
		set => _data = (WledData)value;
	}


	public async Task StartStream(CancellationToken ct) {
		if (Streaming || !Enable) {
			return;
		}

		Log.Debug($"{_data.Tag}::Starting stream: {_data.Id}...");
		_targetSector = _data.TargetSector;
		_ep = IpUtil.Parse(IpAddress, Port);
		if (_ep == null) {
			return;
		}

		// Matrix targets stream over DDP (port 4048) instead of WLED's native realtime port.
		_ddpEp = IpUtil.Parse(IpAddress, DdpPacket.Port);

		await SetLightBrightness(_brightness);
		await SetLightPower(true);
		await ClearLiveOverride();

		// Matrix frames are streamed in lock-step with any other matrix targets via the shared
		// coordinator; register before the first frame so it has a socket + endpoint to use.
		if (_stripMode == StripMode.Matrix && _ddpEp != null) {
			_streamer.Register(Id, _ddpEp, _udpClient, Math.Max(_matrixWidth * _matrixHeight, 1));
			_matrixRegistered = true;
		}

		await FlashColor(Color.Black);
		Streaming = true;
		StartLorWatchdog();
		Log.Debug($"{_data.Tag}::Stream started: {_data.Id}.");
	}

	/// <summary>
	///     Start a low-frequency timer that re-asserts <c>lor=0</c> while streaming. See
	///     <see cref="ClearLiveOverride" /> for why this matters.
	/// </summary>
	private void StartLorWatchdog() {
		_lorWatchdog?.Dispose();
		_lorWatchdog = new Timer(_ => {
			if (Streaming) {
				_ = ClearLiveOverride();
			}
		}, null, LorWatchdogMs, LorWatchdogMs);
	}

	/// <summary>
	///     Clear WLED's live-override flag (<c>lor</c>) so the device applies our realtime data.
	///     If the user touches the WLED app while streaming, WLED sets <c>lor=1</c> and silently
	///     ignores incoming DDP/UDP - this releases that so a stream start always takes effect.
	/// </summary>
	private async Task ClearLiveOverride() {
		try {
			using var content = new StringContent("{\"lor\":0}", Encoding.UTF8, "application/json");
			await HttpClient.PostAsync("http://" + IpAddress + "/json/state", content);
		} catch (Exception e) {
			Log.Debug($"{_data.Tag}::Clear live override failed: {e.Message}");
		}
	}
	
	

	public async Task StopStream() {
		if (!Streaming) {
			return;
		}

		Log.Debug($"{_data.Tag}::Stopping stream...{_data.Id}.");
		Streaming = false;
		_lorWatchdog?.Dispose();
		_lorWatchdog = null;
		if (_matrixRegistered) {
			_streamer.Unregister(Id);
			_matrixRegistered = false;
		}

		await FlashColor(Color.Black);
		await _wLedClient.Post(new StateRequest { On = false });
		await Task.FromResult(true);
		await SetLightPower(false);
		Log.Debug($"{_data.Tag}::Stream stopped: {_data.Id}.");
	}


	public async Task FlashColor(Color color) {
		try {
			if (_stripMode == StripMode.Matrix) {
				var count = Math.Max(_matrixWidth * _matrixHeight, 1);
				await SendMatrixFrameDirect(ColorUtil.FillArray(color, count));
				return;
			}

			var colors = ColorUtil.FillArray(color, _ledCount);
			var cp = new ColorPacket(colors, (UdpStreamMode)_protocol);
			var packet = cp.Encode();
			await _udpClient.SendAsync(packet.ToArray(), packet.Length, _ep).ConfigureAwait(false);
		} catch (Exception e) {
			Log.Debug("Exception flashing color: " + e.Message);
		}
	}



	public Task ReloadData() {
		var oldBrightness = _brightness;
		var dev = DataUtil.GetDevice<WledData>(Id);
		if (dev != null) {
			_data = dev;
		}

		_protocol = _data.Protocol;
		_offset = _data.Offset;
		_brightness = _data.Brightness;
		IpAddress = _data.IpAddress;
		Enable = _data.Enable;
		_stripMode = _data.StripMode;
		_targetSector = _data.TargetSector;
		_multiplier = _data.LedMultiplier;
		if (_multiplier == 0) {
			_multiplier = 1;
		}

		if (oldBrightness != _brightness) {
			SetLightBrightness(_brightness).ConfigureAwait(false);
		}

		_segments = _data.Segments;
		_ledCount = _data.LedCount;

		_matrixWidth = _data.MatrixWidth;
		_matrixHeight = _data.MatrixHeight;
		// Only Mirror/Extend are supported; legacy values map to Mirror.
		_matrixMode = _data.MatrixMode == MatrixMode.Extend ? MatrixMode.Extend : MatrixMode.Mirror;
		_matrixSide = _data.MatrixSide;
		_flipHorizontal = _data.FlipHorizontal;
		_flipVertical = _data.FlipVertical;
		_extendDepth = _data.ExtendDepth;
		_extendFade = _data.ExtendFade;
		return Task.CompletedTask;
	}


	public void Dispose() {
		Dispose(true).ConfigureAwait(true);
		GC.SuppressFinalize(this);
	}


	public async Task SetColors(IReadOnlyList<Color> ledColors, IReadOnlyList<Color> sectorColors) {
		if (!Streaming || !Enable) {
			return;
		}

		var toSend = ledColors.ToArray();
		switch (_stripMode) {
			case StripMode.Matrix:
				// Matrix is driven from the full-frame content grid via the event handler.
				return;
			case StripMode.Single when _targetSector > sectorColors.Count || _targetSector == -1:
				Log.Debug("OOR: " + _targetSector + " vs " + sectorColors.Count);
				return;
			case StripMode.Single:
				toSend = ColorUtil.FillArray(sectorColors[_targetSector - 1], _ledCount).ToArray();
				break;
			case StripMode.Sectored: {
				var output = new Color[_ledCount];
				foreach (var seg in _segments) {
					var cols = ColorUtil.TruncateColors(toSend, seg.Offset, seg.LedCount, seg.Multiplier);
					if (seg.ReverseStrip) {
						cols = Enumerable.Reverse(cols).ToArray();
					}

					var start = seg.Start;
					foreach (var col in cols) {
						if (start >= _ledCount) {
							Log.Warning($"Error, dest color idx is greater than led count: {start}/{_ledCount}");
							continue;
						}

						output[start] = col;
						start++;
					}
				}
				toSend = output;
				break;
			}
			case StripMode.Loop: {
				toSend = ColorUtil.TruncateColors(toSend, _offset, _ledCount, _multiplier);
				toSend = ShiftColors(toSend, _data.ReverseStrip);
				break;
			}
			case StripMode.Normal: {
				toSend = ColorUtil.TruncateColors(toSend, _offset, _ledCount, _multiplier);
				if (_data.ReverseStrip) {
					toSend = Enumerable.Reverse(toSend).ToArray();
				}

				break;
			}
			default:
				Log.Debug("Well, this is weird...");
				break;
		}

		if (_ep == null) {
			Log.Debug("No endpoint.");
			return;
		}

		// Fire-and-forget so a slow/queued WLED socket can't gate the color pipeline / FPS.
		var cp = new ColorPacket(toSend, (UdpStreamMode)_protocol);
		var packet = cp.Encode(255);
		_ = SendDatagram(packet, packet.Length, _ep);
		await Task.CompletedTask;
	}

	private Task SetColors(object sender, ColorSendEventArgs args) {
		if (_stripMode == StripMode.Matrix) {
			return SetMatrixColors(args.MatrixColors, args.MatrixWidth, args.MatrixHeight);
		}

		return SetColors(args.LedColors, args.SectorColors);
	}


	private static Color[] ShiftColors(IReadOnlyList<Color> input, bool reverse) {
		var output = new Color[input.Count];
		var il = output.Length - 1;
		if (!reverse) {
			for (var i = 0; i < input.Count / 2; i++) {
				output[i] = input[i];
				output[il - i] = input[i];
			}
		} else {
			var l = 0;
			for (var i = (input.Count - 1) / 2; i >= 0; i--) {
				output[i] = input[l];
				output[il - i] = input[l];
				l++;
			}
		}


		return output;
	}

	/// <summary>
	///     Map the full-frame content grid onto a 2D matrix and stream it over DDP. Mirror shows
	///     the literal full screen; Extend reflects the chosen screen edge outward across the
	///     panel, fading with distance. Flips reorient the output; WLED applies its own 2D wiring.
	/// </summary>
	private Task SetMatrixColors(IReadOnlyList<Color> matrix, int srcW, int srcH) {
		if (!Streaming || !Enable || _ddpEp == null) {
			return Task.CompletedTask;
		}

		if (matrix.Count == 0 || srcW <= 0 || srcH <= 0 || matrix.Count < srcW * srcH) {
			return Task.CompletedTask;
		}

		var w = _matrixWidth;
		var h = _matrixHeight;
		if (w <= 0 || h <= 0) {
			return Task.CompletedTask;
		}

		EnsureMatrixBuffers(w, h);

		// Scene mode always renders matrix targets as a full-screen mirror so scene playback is
		// deterministic across curtains. In all other modes, honor the device's configured
		// curtain behavior (Mirror vs Extend + side/depth/fade).
		var effectiveMode = ColorService.CurrentMode == DeviceMode.Scene
			? MatrixMode.Mirror
			: _matrixMode;
		if (effectiveMode == MatrixMode.Extend) {
			FillExtend(matrix, srcW, srcH, w, h);
		} else {
			FillMirror(matrix, srcW, srcH, w, h);
		}

		// Copy the logical panel into the wire buffer, applying flips for physical mounting.
		for (var dy = 0; dy < h; dy++) {
			var oy = _flipVertical ? h - 1 - dy : dy;
			for (var dx = 0; dx < w; dx++) {
				var ox = _flipHorizontal ? w - 1 - dx : dx;
				_matrixWire[oy * w + ox] = _panel[dy * w + dx];
			}
		}

		// Hand the mapped frame to the shared coordinator; it owns the synchronized DDP send so
		// this device's PUSH latches together with every other matrix target.
		_streamer.Submit(Id, _matrixWire);
		return Task.CompletedTask;
	}

	/// <summary>
	///     Mirror: map the whole screen 1:1 across the panel (bilinear sampled).
	/// </summary>
	private void FillMirror(IReadOnlyList<Color> matrix, int srcW, int srcH, int w, int h) {
		for (var dy = 0; dy < h; dy++) {
			var v = h <= 1 ? 0f : (float)dy / (h - 1);
			for (var dx = 0; dx < w; dx++) {
				var u = w <= 1 ? 0f : (float)dx / (w - 1);
				_panel[dy * w + dx] = SampleSource(matrix, srcW, srcH, u, v);
			}
		}
	}

	/// <summary>
	///     Extend: reflect the screen's near edge outward across the panel. The panel edge nearest
	///     the screen shows that screen edge; moving away samples progressively inward (a mirror
	///     image), up to <see cref="_extendDepth" />% deep, dimming toward the far edge by
	///     <see cref="_extendFade" />% so it reads as the scene receding rather than a flat stretch.
	/// </summary>
	private void FillExtend(IReadOnlyList<Color> matrix, int srcW, int srcH, int w, int h) {
		var horizontal = _matrixSide is MatrixSide.Left or MatrixSide.Right;
		var depth = Math.Clamp(_extendDepth / 100f, 0.01f, 1f);
		var fade = Math.Clamp(_extendFade / 100f, 0f, 1f);

		for (var dy = 0; dy < h; dy++) {
			for (var dx = 0; dx < w; dx++) {
				float u, v, dist;
				if (horizontal) {
					var t = w <= 1 ? 0f : (float)dx / (w - 1);
					// Right: near edge at dx=0 (screen u=1); Left: near edge at dx=w-1 (screen u=0).
					dist = _matrixSide == MatrixSide.Right ? t : 1f - t;
					u = _matrixSide == MatrixSide.Right ? 1f - dist * depth : dist * depth;
					v = h <= 1 ? 0f : (float)dy / (h - 1);
				} else {
					var t = h <= 1 ? 0f : (float)dy / (h - 1);
					// Bottom: near edge at dy=0 (screen v=1); Top: near edge at dy=h-1 (screen v=0).
					dist = _matrixSide == MatrixSide.Bottom ? t : 1f - t;
					v = _matrixSide == MatrixSide.Bottom ? 1f - dist * depth : dist * depth;
					u = w <= 1 ? 0f : (float)dx / (w - 1);
				}

				var c = SampleSource(matrix, srcW, srcH, u, v);
				var bright = 1f - fade * dist;
				_panel[dy * w + dx] = Color.FromArgb(
					ClampByte(c.R * bright), ClampByte(c.G * bright), ClampByte(c.B * bright));
			}
		}
	}

	/// <summary>Bilinearly sample the source content grid at normalized (u, v) in [0,1].</summary>
	private static Color SampleSource(IReadOnlyList<Color> src, int srcW, int srcH, float u, float v) {
		var fx = Math.Clamp(u, 0f, 1f) * (srcW - 1);
		var fy = Math.Clamp(v, 0f, 1f) * (srcH - 1);
		var x0 = (int)MathF.Floor(fx);
		var y0 = (int)MathF.Floor(fy);
		var x1 = Math.Min(x0 + 1, srcW - 1);
		var y1 = Math.Min(y0 + 1, srcH - 1);
		var fracX = fx - x0;
		var fracY = fy - y0;

		var c00 = src[y0 * srcW + x0];
		var c10 = src[y0 * srcW + x1];
		var c01 = src[y1 * srcW + x0];
		var c11 = src[y1 * srcW + x1];
		return BilerpColor(c00, c10, c01, c11, fracX, fracY);
	}

	private void EnsureMatrixBuffers(int w, int h) {
		if (_wireW == w && _wireH == h) {
			return;
		}

		_matrixWire = new Color[w * h];
		_panel = new Color[w * h];
		_wireW = w;
		_wireH = h;
	}

	/// <summary>
	///     Send a one-off full matrix frame to WLED over DDP directly (used for flashes at stream
	///     start/stop, which run outside the coordinator's send loop). Packets are awaited so the
	///     frame is guaranteed flushed before we power the device off; the last packet carries the
	///     PUSH flag so WLED latches the whole frame at once.
	/// </summary>
	private async Task SendMatrixFrameDirect(Color[] wire) {
		var ep = _ddpEp;
		if (ep == null) {
			return;
		}

		var total = wire.Length;
		var seq = 0;
		for (var start = 0; start < total; start += DdpPacket.MaxPixelsPerPacket) {
			var count = Math.Min(DdpPacket.MaxPixelsPerPacket, total - start);
			var push = start + count >= total;
			seq = seq % 15 + 1;
			var buf = new byte[DdpPacket.MaxPacketBytes];
			var len = DdpPacket.EncodeInto(buf, wire, start, count, push, seq);
			await SendDatagram(buf, len, ep);
		}
	}

	private async Task SendDatagram(byte[] packet, int len, IPEndPoint ep) {
		try {
			await _udpClient.SendAsync(packet, len, ep).ConfigureAwait(false);
		} catch (Exception e) {
			// Throttle so a persistently bad link doesn't flood the log every frame.
			if (_udpWatch.ElapsedMilliseconds > 5000) {
				_udpWatch.Restart();
				Log.Warning($"{_data.Tag}::DDP send to {ep} failed: {e.Message}");
			}
		}
	}

	private static Color BilerpColor(Color c00, Color c10, Color c01, Color c11, float fx, float fy) {
		var r = Lerp(Lerp(c00.R, c10.R, fx), Lerp(c01.R, c11.R, fx), fy);
		var g = Lerp(Lerp(c00.G, c10.G, fx), Lerp(c01.G, c11.G, fx), fy);
		var b = Lerp(Lerp(c00.B, c10.B, fx), Lerp(c01.B, c11.B, fx), fy);
		return Color.FromArgb(ClampByte(r), ClampByte(g), ClampByte(b));
	}

	private static float Lerp(float a, float b, float t) {
		return a + (b - a) * t;
	}

	private static int ClampByte(float v) {
		if (v <= 0f) {
			return 0;
		}

		return v >= 255f ? 255 : (int)(v + 0.5f);
	}

	private void RefreshSystem() {
		ReloadData();
	}

	private async Task SetLightPower(bool on) {
		var res = await _wLedClient.GetState();
		var oc = 0;
		while (res.On != on && oc < 5) {
			await _wLedClient.Post(new StateRequest { On = on });
			// If on is false, send a manual post directly to the device with a JSON body of "live":false
			if (!on) {
				using var content = new StringContent("{\"live\":false}", Encoding.UTF8, "application/json");
				await HttpClient.PostAsync("http://" + IpAddress + "/json/state", content);
			}
			res = await _wLedClient.GetState();
			oc++;
		}
	}

	private async Task SetLightBrightness(int brightness) {
		var scaledBright = brightness / 100f * 255f;
		if (scaledBright > 255) {
			scaledBright = 255;
		}

		await _wLedClient.Post(new StateRequest { Brightness = (byte)scaledBright });
	}

	protected virtual async Task Dispose(bool disposing) {
		if (_disposed) {
			return;
		}

		if (disposing) {
			if (Streaming) {
				await StopStream();
			}

			_lorWatchdog?.Dispose();
			_lorWatchdog = null;
			if (_matrixRegistered) {
				_streamer.Unregister(Id);
				_matrixRegistered = false;
			}

			_udpClient.Dispose();
		}

		_disposed = true;
	}
}