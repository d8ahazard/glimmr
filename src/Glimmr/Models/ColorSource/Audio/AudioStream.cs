﻿#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using ManagedBass;
using Microsoft.Extensions.Hosting;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.Audio {
	public sealed class AudioStream : BackgroundService, IColorSource {
		public bool SendColors {
			set => StreamSplitter.DoSend = value;
		}

		public FrameSplitter StreamSplitter { get; }
		private readonly FrameBuilder _builder;

		private List<AudioData> _devices;
		private float _gain;
		private int _handle;
		private bool _hasDll;
		private AudioMap _map;
		private float _min = .015f;
		private int _recordDeviceIndex;
		private SystemData? _sd;

		public AudioStream(ColorService cs) {
			_devices = new List<AudioData>();
			_map = new AudioMap();
			StreamSplitter = new FrameSplitter(cs);
			_builder = new FrameBuilder(new[] {
				3, 3, 6, 6
			}, true);
			cs.ControlService.RefreshSystemEvent += RefreshSystem;
			RefreshSystem();
		}

		public bool SourceActive => StreamSplitter.SourceActive;


		public void RefreshSystem() {
			_sd = DataUtil.GetSystemData();
			LoadData();
		}

		public Task ToggleStream(CancellationToken ct) {
			SendColors = true;
			try {
				Bass.RecordInit(_recordDeviceIndex);
				_handle = Bass.RecordStart(48000, 2, BassFlags.Float, Update);
				Bass.RecordGetDeviceInfo(_recordDeviceIndex, out _);
				_hasDll = true;
				Log.Information("Recording init completed.");
			} catch (DllNotFoundException) {
				Log.Warning("Bass.dll not found, nothing to do...");
				_hasDll = false;
			}

			if (!_hasDll) {
				Log.Debug("Audio stream unavailable, no bass.dll found.");
				return Task.CompletedTask;
			}

			Log.Debug("Starting audio stream service...");
			Bass.ChannelPlay(_handle);
			return ExecuteAsync(ct);
		}

		protected override Task ExecuteAsync(CancellationToken ct) {
			return Task.Run(async () => {
				while (!ct.IsCancellationRequested) {
					await Task.Delay(1, ct);
				}

				Bass.ChannelStop(_handle);
				Bass.Free();
				Bass.RecordFree();
				SendColors = false;
				Log.Debug("Audio stream service stopped.");
			}, CancellationToken.None);
		}
		
		public Color[] GetColors() {
			return StreamSplitter.GetColors();
		}

		public Color[] GetSectors() {
			return StreamSplitter.GetSectors();
		}


		private void LoadData() {
			_sd = DataUtil.GetSystemData();
			_gain = _sd.AudioGain;
			_min = _sd.AudioMin;
			string rd = _sd.RecDev;
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
				//return false;
			}

			var samples = 2048 * 2;
			var fft = new float[samples]; // fft data buffer
			// Get our FFT for "everything"
			Bass.ChannelGetData(handle, fft, (int) DataFlags.FFT4096 | (int) DataFlags.FFTIndividual);
			var lData = new Dictionary<int, float>();
			var rData = new Dictionary<int, float>();
			var realIndex = 0;

			for (var a = 0; a < samples; a++) {
				var val = FlattenValue(fft[a]);
				//if (val > 0) Log.Information($"Audio val: {val}");
				var freq = FftIndex2Frequency(realIndex, 4096 / 2, 48000);

				if (a % 1 == 0) {
					lData[freq] = val;
				}

				if (a % 2 == 0) {
					rData[freq] = val;
					realIndex++;
				}
			}

			if (_map == null) {
				Log.Warning("No map.");
				return false;
			}

			var sectors = _map.MapColors(lData, rData).ToList();
			var frame = _builder.Build(sectors);
			StreamSplitter.Update(frame).ConfigureAwait(false);

			frame.Dispose();
			return true;
		}

		private float FlattenValue(float input) {
			// Drop anything that's not at least .01
			if (input < _min) {
				return 0;
			}

			input += _gain;
			if (input > 1) {
				input = 1;
			}

			return input;
		}


		private static int FftIndex2Frequency(int index, int length, int sampleRate) {
			return index * sampleRate / length;
		}

		#region intColors

		#endregion

		#region floatColors

		#endregion
	}
}