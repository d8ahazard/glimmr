#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using ManagedBass;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.Audio;

public class AudioStream : ColorSource {
	public bool SendColors {
		set => StreamSplitter.DoSend = value;
	}

	public override bool SourceActive => StreamSplitter.SourceActive;

	public FrameSplitter StreamSplitter { get; }
	private readonly FrameBuilder _builder;

	private List<AudioData> _devices;
	//private float _gain;
	private int _handle;
	private bool _hasDll;
	private AudioMap _map;
	//private float _min = .015f;
	private int _recordDeviceIndex;
	private SystemData? _sd;
	private const int SampleSize = 512;
	private const int SampleFreq = 48000;
	public AudioStream(ColorService cs) {
		_devices = new List<AudioData>();
		_map = new AudioMap();
		StreamSplitter = new FrameSplitter(cs);
		_builder = new FrameBuilder(new[] {
			4, 4, 6, 6
		});
		cs.ControlService.RefreshSystemEvent += RefreshSystem;
		RefreshSystem();
	}

	public override Task Start(CancellationToken ct) {
		RunTask = ExecuteAsync(ct);
		return Task.CompletedTask;
	}


	public sealed override void RefreshSystem() {
		_sd = DataUtil.GetSystemData();
		LoadData();
	}


	protected override Task ExecuteAsync(CancellationToken ct) {
		return Task.Run(async () => {
			SendColors = true;
			try {
				Bass.RecordInit(_recordDeviceIndex);
				// Ensure volume is 100%
				_handle = Bass.RecordStart(SampleFreq, 2, BassFlags.Float, 100, Update);
				Bass.ChannelSetAttribute(_handle, ChannelAttribute.Volume, 1);

				_hasDll = true;
				Log.Information("Recording init completed.");
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
			Bass.ChannelSetAttribute(_handle, ChannelAttribute.Frequency, SampleFreq);
			Bass.ChannelPlay(_handle, true);
			Log.Debug("Audio stream started.");
			while (!ct.IsCancellationRequested) {
				await Task.Delay(TimeSpan.FromMilliseconds(500), CancellationToken.None);
			}

			try {
				Bass.ChannelStop(_handle);
				Bass.Free();
				Bass.RecordFree();
				SendColors = false;
				Log.Debug("Audio stream service stopped.");
			} catch (Exception e) {
				Log.Warning("Exception stopping stream..." + e.Message);
			}

			return Task.CompletedTask;
		}, CancellationToken.None);
	}


	private void LoadData() {
		_sd = DataUtil.GetSystemData();
		// _gain = _sd.AudioGain;
		// _min = _sd.AudioMin;
		var rd = _sd.RecDev;
		_devices = DataUtil.GetCollection<AudioData>("Dev_Audio") ?? new List<AudioData>();
		_map = new AudioMap();
		_recordDeviceIndex = -1;
		_devices = new List<AudioData>();
		try {
			for (var a = 0; Bass.RecordGetDeviceInfo(a, out var info); a++) {
				if (!info.IsEnabled) {
					continue;
				}

				try {
					var ad = new AudioData();
					ad.ParseDevice(info);
					DataUtil.InsertCollection<AudioData>("Dev_Audio", ad).ConfigureAwait(true);
					_devices.Add(ad);
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

					_recordDeviceIndex = a;
				}
			}
		} catch (Exception e) {
			if (e.GetType() == typeof(DllNotFoundException)) {
				_hasDll = false;
				Log.Warning("Unable to find bass.dll, nothing to do.");
				return;
			}
		}

		_devices = DataUtil.GetCollection<AudioData>("Dev_Audio") ?? new List<AudioData>();
	}

	private bool Update(int handle, IntPtr buffer, int length, IntPtr user) {
		if (_map == null) {
			Log.Warning("No map?");
			return true;
		}
		
		var fft = new float[SampleSize]; // fft data buffer
		// Get our FFT for "everything"
		var res = Bass.ChannelGetData(handle, fft, getFlag(SampleSize));
		if (res == -1) {
			Log.Warning("Error getting channel data: " + Bass.LastError);
			return true;
		}

		if (res > 0) {
			var lData = new Dictionary<float, int>();

			for (var a = 0; a < SampleSize; a++) {
				var val = fft[a];
				var y = (int)(Math.Sqrt(val) * 3 * 255 - 4);
				if (y > 255) y = 255;
				if (y < 0) y = 0;
				var freq = FftIndex2Frequency(a, SampleSize, SampleFreq);
				if (y != 0) lData[freq] = y;
			}
			//if (lData.Count > 0) Log.Debug("Ldata: " + JsonConvert.SerializeObject(lData));

			var sectors = _map.MapColors(lData).ToList();
			var frame = _builder.Build(sectors);
			if (frame != null) {
				StreamSplitter.Update(frame).ConfigureAwait(false);
				frame.Dispose();
				return true;
			}
		} else {
			Log.Debug("NO RES.");
		}
		return true;
	}


	private static float FftIndex2Frequency(int index, int length, int sampleRate) {
		return 1f * index * sampleRate / length;
	}

	private static int getFlag(int size) {
		return size switch {
			256 => (int)DataFlags.FFT512,
			512 => (int)DataFlags.FFT1024,
			1024 => (int)DataFlags.FFT2048,
			2048 => (int)DataFlags.FFT4096,
			4096 => (int)DataFlags.FFT8192,
			_ => 0
		};
	}

	#region intColors

	#endregion

	#region floatColors

	#endregion
}