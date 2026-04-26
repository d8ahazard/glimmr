#region

using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Models.ColorSource.Audio;
using Glimmr.Models.ColorSource.Audio.Engine;
using Glimmr.Models.ColorSource.Video;
using Glimmr.Models.Data;
using Glimmr.Models.Frame;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.AudioVideo;

/// <summary>
///     Run video and audio capture in parallel, blend them with the
///     <see cref="AudioReactiveState" /> snapshot from
///     <see cref="AudioStream" />, and emit a single combined color
///     stream. Compared to the previous implementation this uses bass /
///     beat / band data directly instead of reading 'brightness' off the
///     audio frame, so the video stays vibrant during quiet sections and
///     gets accent flashes on beats.
/// </summary>
public class AudioVideoStream : ColorSource {
	public override bool SourceActive => _vs != null && _vs.Splitter.SourceActive;
	public sealed override FrameBuilder? Builder { get; set; }
	public sealed override FrameSplitter Splitter { get; set; }

	private readonly ColorService _cs;
	private AudioStream? _as;
	private Task? _aTask;
	private bool _doSave;
	private SystemData _systemData;
	private VideoStream? _vs;
	private Task? _vTask;

	public AudioVideoStream(ColorService cs) {
		_cs = cs;
		var vS = (ColorSource?)_cs.GetStream(DeviceMode.Video.ToString());
		Splitter = vS != null ? vS.Splitter : new FrameSplitter(cs);
		_systemData = DataUtil.GetSystemData();
		_cs.ControlService.RefreshSystemEvent += RefreshSystem;
		_cs.FrameSaveEvent += TriggerSave;
	}

	public override Task Start(CancellationToken ct) {
		RefreshSystem();
		var aS = _cs.GetStream(DeviceMode.Audio.ToString());
		var vS = _cs.GetStream(DeviceMode.Video.ToString());
		if (aS != null) {
			_as = (AudioStream)aS;
		}

		if (vS != null) {
			_vs = (VideoStream)vS;
		}

		if (_vs != null && _as != null) {
			Log.Debug("Starting video stream...");
			_vTask = _vs.Start(ct);
			_vs.SendColors = false;
			Log.Debug("Starting audio stream...");
			_aTask = _as.Start(ct);
			_as.SendColors = false;
		} else {
			Log.Warning("Unable to acquire audio or video stream.");
			return Task.CompletedTask;
		}

		Log.Debug("Starting main av loop...");
		RunTask = ExecuteAsync(ct);
		return Task.CompletedTask;
	}

	public override void RefreshSystem() {
		_systemData = DataUtil.GetSystemData();
	}

	private void TriggerSave() {
		_doSave = true;
	}

	protected override Task ExecuteAsync(CancellationToken ct) {
		return Task.Run(async () => {
			while (!ct.IsCancellationRequested) {
				if (_vs == null || _as == null) {
					if (_vs == null) {
						Log.Warning("Video stream is null.");
					}

					if (_as == null) {
						Log.Warning("Audio stream is null.");
					}

					await Task.Delay(500, ct);
					continue;
				}

				var vCols = _vs.Splitter.GetColors();
				var vSecs = _vs.Splitter.GetSectors();
				var aCols = _as.Splitter.GetColors();
				var aSecs = _as.Splitter.GetSectors();
				if (vCols.Length == 0 || vCols.Length != aCols.Length || vSecs.Length == 0 ||
				    vSecs.Length != aSecs.Length) {
					await Task.Delay(16, ct);
					continue;
				}

				var state = _as.ReactiveState;

				var oCols = new Color[_systemData.LedCount];
				var oSecs = new Color[_systemData.SectorCount];

				BlendInto(vCols, aCols, oCols, state, isLed: true);
				BlendInto(vSecs, aSecs, oSecs, state, isLed: false);

				await _cs.SendColors(oCols, oSecs);

				if (_doSave && _cs.ControlService.SendPreview) {
					_doSave = false;
					_vs.Splitter.MergeFrame(oCols, oSecs);
				}

				await Task.Delay(16, ct);
			}

			if (_vs != null) {
				_vs.SendColors = true;
			}

			if (_as != null) {
				_as.SendColors = true;
			}

			Log.Debug("AV stream service stopped.");
		}, CancellationToken.None);
	}

	/// <summary>
	///     Blend the video color with the audio reactive snapshot.
	///     <list type="bullet">
	///         <item>
	///             RMS sets a global brightness floor so quiet sections are still
	///             watchable.
	///         </item>
	///         <item>
	///             Per-slot band energy (read from the audio path's 20-color
	///             output) modulates brightness around the perimeter so highs
	///             pop on the top and bass pumps the bottom.
	///         </item>
	///         <item>
	///             Beat pulses add a brief saturation/brightness kick.
	///         </item>
	///     </list>
	/// </summary>
	private static void BlendInto(Color[] video, Color[] audio, Color[] output,
		AudioReactiveState state, bool isLed) {
		var rms = state.Rms;
		var beat = state.BeatPulse;
		var globalFloor = 0.45f + 0.5f * rms;
		var beatBoost = 0.35f * beat;
		var len = Math.Min(video.Length, audio.Length);

		for (var i = 0; i < len; i++) {
			var v = video[i];

			// Use the audio renderer's per-slot brightness as a position-aware
			// modulator (audio renderer already places bass at the bottom etc).
			var ab = ColorBrightness(audio[i]);
			var modulator = globalFloor + ab * (1f - globalFloor) + beatBoost;
			modulator = Math.Clamp(modulator, 0f, 1f);

			byte r = (byte)Math.Clamp(v.R * modulator, 0, 255);
			byte g = (byte)Math.Clamp(v.G * modulator, 0, 255);
			byte b = (byte)Math.Clamp(v.B * modulator, 0, 255);

			output[i] = Color.FromArgb(255, r, g, b);
		}

		// Pad in case lengths differ (defensive).
		for (var i = len; i < output.Length; i++) {
			output[i] = i < video.Length ? video[i] : Color.FromArgb(0, 0, 0, 0);
		}

		_ = isLed; // reserved for future divergent treatment of led vs sector arrays
	}

	private static float ColorBrightness(Color c) {
		// Perceptual luminance.
		var f = (0.299f * c.R + 0.587f * c.G + 0.114f * c.B) / 255f;
		return Math.Clamp(f, 0f, 1f);
	}
}
