#region

using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.ColorSource.Audio.Engine;
using Glimmr.Models.Data;
using Glimmr.Models.Frame;
using Glimmr.Models.Util;
using Glimmr.Services;
using ManagedBass;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.Audio;

/// <summary>
///     Capture-side orchestrator for the audio visualizer. Talks to BASS,
///     pulls FFT magnitudes, hands them to <see cref="AudioMap" /> (which
///     owns the analyzer + active effect), and pushes the resulting 20
///     colors into the standard frame pipeline.
/// </summary>
public class AudioStream : ColorSource {
	public bool SendColors {
		set => Splitter.DoSend = value;
	}

	public override bool SourceActive => Splitter.SourceActive;
	public sealed override FrameBuilder? Builder { get; set; }
	public sealed override FrameSplitter Splitter { get; set; }

	private const int SampleSize = 512;

	/// <summary>Seconds of sustained silence before the output is forced to black.</summary>
	private const double SilenceTimeoutSeconds = 2.5;

	private readonly object _lastFrameLock = new();
	private readonly object _handoffLock = new();
	private readonly float[] _fftBuffer = new float[SampleSize];
	private readonly Color[] _handoffColors = new Color[PerimeterLayout.SlotCount];
	private readonly SemaphoreSlim _frameSignal = new(0, 1);
	private CancellationToken? _ct;
	private DateTime _lastFrameTimeUtc;
	private DateTime _lastSilenceTimeUtc;
	private bool _hasPending;
	private bool _silenced;
	private int _handle;
	private bool _hasDll;
	private AudioMap _map;
	private int _recordDeviceIndex;
	private bool _restart;
	private bool _running;
	private int _sampleFreq = 48000;
	private SystemData? _sd;
	private bool _updating;

	public AudioStream(ColorService cs) {
		_map = new AudioMap();
		Splitter = new FrameSplitter(cs);
		SendColors = true;
		Builder = new FrameBuilder(new[] { 4, 4, 6, 6 });
		cs.ControlService.RefreshSystemEvent += RefreshSystem;
		RefreshSystem();
	}

	/// <summary>
	///     The most recent reactive state observed by the analyzer. Exposed
	///     so the audio+video stream can blend richer features (beat,
	///     bass, centroid) into video frames instead of reading only
	///     brightness from the rendered colors.
	/// </summary>
	public AudioReactiveState ReactiveState => _map.State;

	public override Task Start(CancellationToken ct) {
		_ct = ct;
		RunTask = ExecuteAsync(ct);
		return Task.CompletedTask;
	}

	public sealed override void RefreshSystem() {
		_sd = DataUtil.GetSystemData();
		var idx = _recordDeviceIndex;
		LoadData();
		_map.Refresh();
		if (idx == _recordDeviceIndex || _ct == null) {
			return;
		}

		_restart = true;
		_ = RestartWhenStopped();
	}

	private async Task RestartWhenStopped() {
		while (_running) {
			await Task.Delay(TimeSpan.FromSeconds(1));
		}

		try {
			if (_ct == null) {
				Log.Debug("RestartWhenStopped: cancellation token is null, aborting restart.");
				return;
			}

			_restart = false;
			RunTask = ExecuteAsync((CancellationToken)_ct);
		} catch (Exception e) {
			Log.Warning("Exception: " + e.Message + " at " + e.StackTrace + " via " + e.Source);
		}
	}

	protected override Task ExecuteAsync(CancellationToken ct) {
		_ct = ct;
		Log.Debug("Using bass ver: " + Bass.Version);
		return Task.Run(async () => {
			try {
				Bass.RecordInit(_recordDeviceIndex);
				const int period = 33;
				_handle = Bass.RecordStart(_sampleFreq, 2, BassFlags.Default, period, UpdateAudio);
				_hasDll = true;
				Log.Information(
					$"Recording init completed for {_sd?.RecDev ?? ""} ({_recordDeviceIndex})");
			} catch (DllNotFoundException) {
				Log.Warning("Bass.dll not found, audio visualizer disabled.");
				_hasDll = false;
			} catch (Exception e) {
				Log.Debug("Audio init exception: " + e.Message);
			}

			if (!_hasDll) {
				return Task.CompletedTask;
			}

			Log.Debug("Starting audio stream service...");
			_running = true;
			_lastFrameTimeUtc = DateTime.UtcNow;
			// Frame building + splitter updates run on a worker so the BASS recording
			// callback only does cheap DSP and never blocks on device I/O.
			var renderTask = Task.Run(() => RenderLoop(ct), CancellationToken.None);
			Bass.ChannelPlay(_handle, true);
			while (!ct.IsCancellationRequested && !_restart) {
				await Task.Delay(TimeSpan.FromMilliseconds(500), CancellationToken.None);
			}

			if (_restart) {
				Log.Debug("Audio stream restart requested.");
			}

			try {
				Bass.ChannelStop(_handle);
				Bass.Free();
				Bass.RecordFree();
				Log.Debug("Audio stream service stopped.");
			} catch (Exception e) {
				Log.Warning("Exception stopping stream: " + e.Message);
			}

			_running = false;
			await renderTask.ConfigureAwait(false);
			return Task.CompletedTask;
		}, CancellationToken.None);
	}

	/// <summary>
	///     Consumes the latest colors produced by the BASS callback and pushes them
	///     through the frame pipeline. Latest-wins: if a frame is still being processed
	///     when new colors arrive, the older colors are simply overwritten.
	/// </summary>
	private async Task RenderLoop(CancellationToken ct) {
		var local = new Color[PerimeterLayout.SlotCount];
		while (!ct.IsCancellationRequested && !_restart && _running) {
			try {
				await _frameSignal.WaitAsync(TimeSpan.FromMilliseconds(250), CancellationToken.None)
					.ConfigureAwait(false);
				bool hasFrame;
				lock (_handoffLock) {
					hasFrame = _hasPending;
					if (hasFrame) {
						Array.Copy(_handoffColors, local, local.Length);
						_hasPending = false;
					}
				}

				if (hasFrame) {
					await RenderColors(local).ConfigureAwait(false);
					continue;
				}

				// No new audio frame: check whether we've been silent long enough to
				// force the output to black.
				var silentSince = _lastSilenceTimeUtc;
				if (_silenced || silentSince == default ||
				    (DateTime.UtcNow - silentSince).TotalSeconds < SilenceTimeoutSeconds) {
					continue;
				}

				_silenced = true;
				await RenderColors(_map.Silence()).ConfigureAwait(false);
			} catch (Exception e) {
				Log.Warning("Audio render loop exception: " + e.Message);
			}
		}
	}

	private async Task RenderColors(Color[] colors) {
		var frame = Builder?.Build(colors);
		if (frame == null) {
			return;
		}

		await Splitter.Update(frame).ConfigureAwait(false);
		frame.Dispose();
	}

	private bool UpdateAudio(int handle, IntPtr buffer, int length, IntPtr user) {
		return !_restart && ProcessHandle(handle);
	}

	private void LoadData() {
		_sd = DataUtil.GetSystemData();
		var rd = _sd.RecDev;
		_recordDeviceIndex = -1;
		try {
			for (var a = 0; Bass.RecordGetDeviceInfo(a, out var info); a++) {
				if (!info.IsEnabled) {
					continue;
				}

				try {
					var ad = new AudioData();
					ad.ParseDevice(info);
					DataUtil.InsertCollection<AudioData>("Dev_Audio", ad).ConfigureAwait(true);
				} catch (Exception e) {
					Log.Warning("Error loading audio device info: " + e.Message);
				}

				if (rd == null && a == 0) {
					DataUtil.SetItem("RecDev", info.Name);
					rd = info.Name;
				} else {
					if (rd != info.Name) {
						continue;
					}

					var freq = AudioInfo.ChannelFrequency(a);
					if (freq != -1) {
						_sampleFreq = freq;
					}

					_recordDeviceIndex = a;
				}
			}
		} catch (Exception e) {
			if (e.GetType() == typeof(DllNotFoundException)) {
				_hasDll = false;
				Log.Warning("Unable to find bass.dll, audio visualizer disabled.");
			}
		}
	}

	private bool ProcessHandle(int handle) {
		if (!_running) {
			return false;
		}

		if (_updating) {
			return true;
		}

		_updating = true;

		try {
			DateTime now;
			float deltaSeconds;
			lock (_lastFrameLock) {
				now = DateTime.UtcNow;
				deltaSeconds = (float)(now - _lastFrameTimeUtc).TotalSeconds;
				if (deltaSeconds <= 0f || deltaSeconds > 1f) {
					deltaSeconds = 0.033f;
				}

				_lastFrameTimeUtc = now;
			}

			// BASS interleaves L/R; pull each channel separately so we have
			// stereo balance even though we only send a mono FFT downstream.
			var levels = Bass.ChannelGetLevel(handle);
			var left = (short)(levels & 0xFFFF);
			var right = (short)((levels >> 16) & 0xFFFF);
			var leftAbs = MathF.Abs(left);
			var rightAbs = MathF.Abs(right);
			var balance = leftAbs + rightAbs > 1f
				? (rightAbs - leftAbs) / (leftAbs + rightAbs)
				: 0f;

			var res = Bass.ChannelGetData(handle, _fftBuffer, (int)GetFlag(SampleSize));

			Color[] colors;
			if (res > 0) {
				colors = _map.Process(_fftBuffer, _sampleFreq, deltaSeconds, balance);
				if (leftAbs + rightAbs < 700f) {
					if (_lastSilenceTimeUtc == default) {
						_lastSilenceTimeUtc = now;
					}
				} else {
					_lastSilenceTimeUtc = default;
					_silenced = false;
				}
			} else if (res == 0) {
				_updating = false;
				return true;
			} else {
				Log.Warning("Error getting channel data: " + Bass.LastError);
				_updating = false;
				return true;
			}

			// Hand the colors to the render worker; the callback never touches the
			// frame pipeline directly.
			lock (_handoffLock) {
				Array.Copy(colors, _handoffColors, _handoffColors.Length);
				_hasPending = true;
			}

			if (_frameSignal.CurrentCount == 0) {
				try {
					_frameSignal.Release();
				} catch (SemaphoreFullException) {
					// Worker already signaled; latest colors win.
				}
			}
		} catch (Exception e) {
			Log.Warning("Audio frame processing failed: " + e.Message);
		} finally {
			_updating = false;
		}

		return true;
	}

	private static DataFlags GetFlag(int size) {
		return size switch {
			256 => DataFlags.FFT512,
			512 => DataFlags.FFT1024,
			1024 => DataFlags.FFT2048,
			2048 => DataFlags.FFT4096,
			4096 => DataFlags.FFT8192,
			_ => 0
		};
	}
}
