#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Data;
using Glimmr.Models.Frame;
using Glimmr.Models.Util;
using Glimmr.Services;
using ManagedBass;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.Audio;

public class AudioStream : ColorSource {
	public bool SendColors {
		set => Splitter.DoSend = value;
	}

	public override bool SourceActive => Splitter.SourceActive;
	public sealed override FrameBuilder? Builder { get; set; }

	public sealed override FrameSplitter Splitter { get; set; }
	private const int SampleFreq = 48000;
	private const int SampleSize = 512;
	private CancellationToken? _ct;
	private int _cutoff;
	private Dictionary<float, int> _frameData;

	//private float _gain;
	private int _handle;
	private bool _hasDll;
	private AudioMap _map;

	private double _maxVal;

	//private float _min = .015f;
	private int _recordDeviceIndex;
	private bool _restart;
	private bool _running;
	private SystemData? _sd;
	private bool _updating;

	public AudioStream(ColorService cs) {
		_frameData = new Dictionary<float, int>();
		_map = new AudioMap();
		Splitter = new FrameSplitter(cs);
		SendColors = true;
		Builder = new FrameBuilder(new[] {
			4, 4, 6, 6
		});
		cs.ControlService.RefreshSystemEvent += RefreshSystem;
		RefreshSystem();
	}

	public override Task Start(CancellationToken ct) {
		_ct = ct;
		RunTask = ExecuteAsync(ct);
		return Task.CompletedTask;
	}


	public sealed override void RefreshSystem() {
		_sd = DataUtil.GetSystemData();
		_cutoff = _sd.AudioCutoff;
		var idx = _recordDeviceIndex;
		LoadData();
		if (idx == _recordDeviceIndex || _ct == null) {
			return;
		}

		_restart = true;
		while (_running) {
			Task.Delay(TimeSpan.FromSeconds(1));
		}

		try {
			if (_ct == null) {
				Log.Debug("NULL CT!");
				return;
			}

			RunTask = ExecuteAsync((CancellationToken)_ct);
		} catch (Exception e) {
			Log.Warning("Exception: " + e.Message + " at " + e.StackTrace + " via " + e.Source);
		}

		_restart = false;
	}


	protected override Task ExecuteAsync(CancellationToken ct) {
		_ct = ct;
		var ver = Bass.Version;
		Log.Debug("Using bass ver: " + ver);
		return Task.Run(async () => {
			try {
				Bass.RecordInit(_recordDeviceIndex);
				var period = 33;
				_handle = Bass.RecordStart(SampleFreq, 2, BassFlags.Default, period, UpdateAudio);
				_hasDll = true;
				Log.Information($"Recording init completed for {_sd?.RecDev ?? ""} ({_recordDeviceIndex})");
			} catch (DllNotFoundException) {
				Log.Warning("Bass.dll not found, nothing to do...");
				_hasDll = false;
			} catch (Exception e) {
				Log.Debug("Generic exception: " + e.Message);
			}

			if (!_hasDll) {
				Log.Debug("Audio stream unavailable, no bass.dll found.");
				return Task.CompletedTask;
			}

			Log.Debug("Starting audio stream service...");
			_running = true;
			Bass.ChannelPlay(_handle, true);
			Log.Debug("Audio stream started.");
			while (!ct.IsCancellationRequested && !_restart) {
				await Task.Delay(TimeSpan.FromMilliseconds(500), CancellationToken.None);
			}

			if (_restart) {
				Log.Debug("Restarting...");
			}

			try {
				Bass.ChannelStop(_handle);
				Bass.Free();
				Bass.RecordFree();
				Log.Debug("Audio stream service stopped.");
			} catch (Exception e) {
				Log.Warning("Exception stopping stream..." + e.Message);
			}

			_running = false;
			return Task.CompletedTask;
		}, CancellationToken.None);
	}

	private bool UpdateAudio(int handle, IntPtr buffer, int length, IntPtr user) {
		return !_restart && ProcessHandle(handle);
	}


	private void LoadData() {
		_sd = DataUtil.GetSystemData();
		_map = new AudioMap();
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
					Log.Warning("Error loading devices: " + e.Message);
				}

				if (rd == null && a == 0) {
					DataUtil.SetItem("RecDev", info.Name);
					rd = info.Name;
				} else {
					if (rd != info.Name) {
						continue;
					}

					Log.Debug("Setting index to " + a);
					_recordDeviceIndex = a;
				}
			}
		} catch (Exception e) {
			if (e.GetType() == typeof(DllNotFoundException)) {
				_hasDll = false;
				Log.Warning("Unable to find bass.dll, nothing to do.");
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
		var lData = new Dictionary<float, int>();
		var level = Bass.ChannelGetLevel(handle);
		var fft = new float[SampleSize]; // fft data buffer
		// Get our FFT for "everything"
		var res = Bass.ChannelGetData(handle, fft, (int)getFlag(SampleSize));
		//Log.Debug("FFT: " + JsonConvert.SerializeObject(fft));
		if (level < 700) {
			_frameData = lData;
			if (_maxVal > 0) {
				_maxVal--;
			}
		} else {
			switch (res) {
				case -1:
					Log.Warning("Error getting channel data: " + Bass.LastError);
					_updating = false;
					return true;
				case 0:
					_updating = false;
					return true;
				case > 0: {
					for (var a = 0; a < SampleSize; a++) {
						var val = fft[a];
						if (float.IsNaN(val) || float.IsInfinity(val)) {
							val = 0;
						}

						var freq = FftIndex2Frequency(a, SampleSize, SampleFreq);

						var y = Math.Sqrt(val) * 3 * 255 - 4;
						if (y > 255) {
							y = 255;
						}

						if (y < 0) {
							y = 0;
						}


						if (_frameData.ContainsKey(freq)) {
							var prev = _frameData[freq];
							if (y < prev) {
								y = prev - 8;
							}
						}

						if (y == 0) {
							continue;
						}

						if (y > _maxVal) {
							_maxVal = y;
						}

						lData[freq] = (int)FlattenValue(y);
					}

					if (lData.Count > 0) {
						_frameData = lData;
					} else {
						if (_maxVal > 0) {
							_maxVal--;
						}
					}

					break;
				}
				default:
					Log.Debug("NO RES: " + res / SampleSize);
					break;
			}
		}

		var sectors = _map.MapColors(_frameData).ToList();
		var frame = Builder?.Build(sectors);
		if (frame != null) {
			Splitter.Update(frame).ConfigureAwait(false);
			frame.Dispose();
		}

		_updating = false;
		return true;
	}

	private double FlattenValue(double v) {
		if (v <= _cutoff) {
			v = 0;
		}

		return v / _maxVal * 255;
	}

	private static float FftIndex2Frequency(int index, int length, int sampleRate) {
		return 0.5f * index * sampleRate / length;
	}

	private static DataFlags getFlag(int size) {
		return size switch {
			256 => DataFlags.FFT512,
			512 => DataFlags.FFT1024,
			1024 => DataFlags.FFT2048,
			2048 => DataFlags.FFT4096,
			4096 => DataFlags.FFT8192,
			_ => 0
		};
	}

	#region intColors

	#endregion

	#region floatColors

	#endregion
}