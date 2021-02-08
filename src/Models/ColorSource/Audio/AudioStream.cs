using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using ManagedBass;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Serilog;
using Color = System.Drawing.Color;

namespace Glimmr.Models.ColorSource.Audio {
	public sealed class AudioStream : BackgroundService {
		
		public bool SourceActive { get; set; }
		public bool SendColors { get; set; }
		private bool _enable;
		private List<AudioData> _devices;
		private int _recordDeviceIndex;
		private int _sectorCount;
		private float _gain;
		private float _min = .015f;
		private readonly ColorService _cs;
		private readonly int _handle;
		private SystemData _sd;
		private AudioMap _map;

		public List<Color> Colors { get; private set; }
		public List<Color> Sectors { get; private set; }
		

		public AudioStream(ColorService cs) {
			_cs = cs;
			_cs.AddStream("audio", this);
			LoadData().ConfigureAwait(true);
			Log.Debug("Bass init...");
			Bass.RecordInit(_recordDeviceIndex);
			Log.Debug("Done");
			Log.Debug("Starting stream with device " + _recordDeviceIndex);
			_handle = Bass.RecordStart(48000, 2, BassFlags.Float, Update);
			Log.Debug("Error check: " + Bass.LastError);
			Bass.RecordGetDeviceInfo(_recordDeviceIndex, out var info3);
			Log.Debug("Loaded: " + JsonConvert.SerializeObject(info3));
		}
		
		protected override Task ExecuteAsync(CancellationToken ct) {
			Log.Debug("Starting audio stream...");
			return Task.Run(async () => {
				while (!ct.IsCancellationRequested) {
					await Task.Delay(1,ct);
				}
				Bass.ChannelStop(_handle);
				Bass.Free();
				Bass.RecordFree();
			}, CancellationToken.None);
		}
		
		public void ToggleStream(bool enable = false) {
			if (enable) {
				SendColors = true;
				Bass.ChannelPlay(_handle);
			} else {
				SendColors = false;
				Bass.ChannelPause(_handle);
			}
			_enable = enable;
		}


		private async Task LoadData() {
			Log.Debug("Reloading audio data");
			_sd = DataUtil.GetObject<SystemData>("SystemData");
			_sectorCount = (_sd.VSectors + _sd.HSectors) * 2 - 4;
			_gain = _sd.AudioGain;
			Colors = ColorUtil.EmptyList(_sd.LedCount);
			Sectors = ColorUtil.EmptyList(_sectorCount);
			_min = _sd.AudioMin;
			_devices = DataUtil.GetCollection<AudioData>("Dev_Audio") ?? new List<AudioData>();
			_map = new AudioMap();
			var scene = new AudioScene();
			Log.Debug("AudioScene: " + JsonConvert.SerializeObject(scene));
			_recordDeviceIndex = -1;
			
			
			string rd = DataUtil.GetItem("RecDev");
			_devices = new List<AudioData>();
			for (var a = 0; Bass.RecordGetDeviceInfo(a, out var info); a++) {
				Log.Debug("Audio device: " + JsonConvert.SerializeObject(info));
				if (!info.IsEnabled) continue;
				try {
					var ad = new AudioData();
					ad.ParseDevice(info);
					await DataUtil.InsertCollection<AudioData>("Dev_Audio", ad);
					_devices.Add(ad);
				} catch (Exception e) {
					Log.Warning("Error loading devices: " + e.Message);
				}

				if (rd == null && a == 0) {
					DataUtil.SetItem("RecDev", info.Name);
					rd = info.Name;
				} else {
					if (rd != info.Name) continue;
					Log.Debug($"Selecting recording device index {a}: {info.Name}");
					_recordDeviceIndex = a;
				}
			}
			_devices = DataUtil.GetCollection<AudioData>("Dev_Audio") ?? new List<AudioData>();
		}
		
		
		

		public void Refresh() {
			LoadData().ConfigureAwait(true);
		}

		
		private bool Update(int handle, IntPtr buffer, int length, IntPtr user) {
			if (!_enable) return true;
			var samples = 2048 * 2;
			var fft = new float[samples]; // fft data buffer
			// Get our FFT for "everything"
			Bass.ChannelGetData(handle, fft, (int) DataFlags.FFT4096 | (int) DataFlags.FFTIndividual);
			var lData = new Dictionary<int, float>();
			var rData = new Dictionary<int, float>();
			var realIndex = 0;

			for (var a = 0; a < samples; a++) {
				var val = FlattenValue(fft[a]);
				var freq = FftIndex2Frequency(realIndex, 4096 / 2, 48000);
				if (val <= .01) {
					continue;
				}

				if (a % 1 == 0) {
					lData[freq] = val;
				}

				if (a % 2 == 0) {
					rData[freq] = val;
					realIndex++;
				}
			}

			var lAmps = SortChannels(lData);
			var rAmps = SortChannels(rData);
			Sectors = ColorUtil.EmptyList(Sectors.Count);
			Colors = ColorUtil.EmptyList(Colors.Count);
			var prev = SourceActive;
			//Log.Debug("Lamps: " + JsonConvert.SerializeObject(lAmps));
			SourceActive = lAmps.Count != 0 || rAmps.Count != 0;
			if (prev != SourceActive) {
				
			}
			if (SourceActive) {
				Log.Debug("Audio input detected!");
			} else {
				//Log.Debug("No audio input...");
			}
			Sectors = _map.MapColors(lAmps, rAmps).ToList();
			Colors = ColorUtil.SectorsToleds(Sectors.ToList(), _sd);
			if (SendColors) {
				//_cs.SendColors(this, new DynamicEventArgs(Colors, Sectors)).ConfigureAwait(false);
				_cs.SendColors(Colors, Sectors, 0);
			}
			return true;
		}

		private float FlattenValue(float input) {
			// Drop anything that's not at least .01
			if (input < _min) {
				return 0;
			}

			input += _gain;
			if (input > 1) input = 1;
			return input;
		}

		#region intColors

		#endregion

		#region floatColors

		private Dictionary<int, KeyValuePair<float, float>> SortChannels(Dictionary<int, float> cData) {
			
			var cValues = new Dictionary<int, KeyValuePair<float,float>>();
			var steps = new[] {30, 60, 125, 250, 500, 1000, 2000};
			foreach (var step in steps) {
				var next = step == 60 ? 125 : step * 2;
				float range = next - step;
				var stepMax = 0f;
				foreach (var (frequency, amplitude) in cData) {
					if (frequency < step || frequency >= next) {
						continue;
					}

					if (amplitude > stepMax) {
						stepMax = amplitude;
					}
				}

				var frequencies = new List<float>();
				foreach (var (frequency, amplitude) in cData) {
					if (frequency < step || frequency >= next) {
						continue;
					}
					if (amplitude != stepMax) {
						continue;
					}

					var avg = (frequency - step) / range;
					frequencies.Add(avg);
				}

				if (frequencies.Count <= 0) {
					continue;
				}

				var sum = frequencies.Sum();
				sum /= frequencies.Count;
				Log.Debug("Setting " + step + " to " + sum);
				cValues[step] = new KeyValuePair<float, float>(sum, stepMax);
			}
			return cValues;
		}


		#endregion


		private static int FftIndex2Frequency(int index, int length, int sampleRate) {
			return index * sampleRate / length;
		}
	}
}