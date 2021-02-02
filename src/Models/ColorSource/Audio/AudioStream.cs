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
using Color = System.Drawing.Color;

namespace Glimmr.Models.ColorSource.Audio {
	public sealed class AudioStream : IColorSource, IDisposable {
		
		public bool SourceActive { get; set; }
		public bool SendColors { get; set; }
		private bool _disposed;
		private List<AudioData> _devices;
		private int _recordDeviceIndex;
		private float _gain;
		private float _min = .015f;
		private readonly ColorService _cs;
		private SystemData _sd;
		private AudioMap _map;

		public List<Color> Colors { get; private set; }
		public List<Color> Sectors { get; private set; }
		

		public AudioStream(ColorService cs) {
			_cs = cs;
			Bass.Init();
		}

		private async Task LoadData() {
			Log.Debug("Reloading audio data");
			_sd = DataUtil.GetObject<SystemData>("SystemData");
			_gain = _sd.AudioGain;
			Colors = ColorUtil.EmptyList(_sd.LedCount);
			Sectors = ColorUtil.EmptyList(28);
			_min = _sd.AudioMin;
			_devices = DataUtil.GetCollection<AudioData>("Dev_Audio") ?? new List<AudioData>();
			_map = new AudioMap(_sd.AudioMap);
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
		
		public void StartStream(CancellationToken ct) {
			LoadData().ConfigureAwait(true);
			_recordDeviceIndex = 1;
			if (_recordDeviceIndex != -1) {
				Log.Debug("Starting stream with device " + _recordDeviceIndex);
				Bass.RecordInit(_recordDeviceIndex);
				Bass.RecordSetInput(_recordDeviceIndex, InputFlags.On, 1);
				Bass.RecordStart(48000, 2, BassFlags.Float, Update);
				DeviceInfo info2 = new DeviceInfo();
				Bass.RecordGetDeviceInfo(_recordDeviceIndex, out info2);
				Log.Debug("Loaded: " + JsonConvert.SerializeObject(info2));
				while (!ct.IsCancellationRequested) {
					Task.Delay(1,ct);
				}
				if (_recordDeviceIndex != -1) {
					Bass.Free();
					Bass.RecordFree();
				}

				Log.Debug("Audio stream canceled.");
			} else {
				Log.Debug("No recording device available.");
			}
		}

		public void StopStream() {
			//throw new NotImplementedException();
			
		}


		public void Refresh() {
			LoadData().ConfigureAwait(true);
		}

		
		private bool Update(int handle, IntPtr buffer, int length, IntPtr user) {
			var samples = 2048 * 2;
			var fft = new float[samples]; // fft data buffer
			// Get our FFT for "everything"
			Bass.ChannelGetData(handle, fft, (int) DataFlags.FFT4096 | (int) DataFlags.FFTIndividual);
			var lData = new Dictionary<int, float>();
			var rData = new Dictionary<int, float>();
			var realIndex = 0;
			
			for (var a = 0; a < samples; a++) {
				var val = FlattenValue(fft[a]);
				var freq = FftIndex2Frequency(realIndex, 4096, 48000);
				if (val <= .01) {
					//continue;
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
			SourceActive = lAmps.Count != 0 || rAmps.Count != 0;
			if (prev != SourceActive && prev == false) {
				Log.Debug("Audio input detected!");
			}
			Sectors = _map.MapColors(lAmps, rAmps, 28).ToList();
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
					if (Math.Abs(amplitude - stepMax) > float.MinValue) {
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
				cValues[step] = new KeyValuePair<float, float>(sum, stepMax);
			}
			return cValues;
		}


		#endregion


		private static int FftIndex2Frequency(int index, int length, int sampleRate) {
			return index * sampleRate / length;
		}


		public void Dispose() {
			Dispose(true);
		}


		private void Dispose(bool disposing) {
			if (_disposed) return;

			if (disposing) {
			}

			_disposed = true;
		}

		
	}
}