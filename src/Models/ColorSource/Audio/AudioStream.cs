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
			try {
				Log.Debug("Bass init...");
				Bass.RecordInit(_recordDeviceIndex);
				Log.Debug("Done");
				Log.Debug("Starting stream with device " + _recordDeviceIndex);
				_handle = Bass.RecordStart(48000, 2, BassFlags.Float, Update);
				Log.Debug("Error check: " + Bass.LastError);
				Bass.RecordGetDeviceInfo(_recordDeviceIndex, out var info3);
				Log.Debug("Loaded: " + JsonConvert.SerializeObject(info3));
			} catch (DllNotFoundException) {
				Log.Warning("Bass not found, nothing to do...");
			}
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
				Refresh();
				SendColors = true;
				Bass.ChannelPlay(_handle);
			} else {
				SendColors = false;
				Bass.ChannelPause(_handle);
			}
			_enable = enable;
		}


		private async Task LoadData() {
			_sd = DataUtil.GetObject<SystemData>("SystemData");
			_sectorCount = (_sd.VSectors + _sd.HSectors) * 2 - 4;
			_gain = _sd.AudioGain;
			Colors = ColorUtil.EmptyList(_sd.LedCount);
			Sectors = ColorUtil.EmptyList(_sectorCount);
			_min = _sd.AudioMin;
			_devices = DataUtil.GetCollection<AudioData>("Dev_Audio") ?? new List<AudioData>();
			_map = new AudioMap();
			_recordDeviceIndex = -1;
			string rd = DataUtil.GetItem("RecDev");
			_devices = new List<AudioData>();
			for (var a = 0; Bass.RecordGetDeviceInfo(a, out var info); a++) {
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
			if (_map == null) return false;
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
				
				if (a % 1 == 0) {
					lData[freq] = val;
				}

				if (a % 2 == 0) {
					rData[freq] = val;
					realIndex++;
				}
			}

			Sectors = ColorUtil.EmptyList(Sectors.Count);
			Colors = ColorUtil.EmptyList(Colors.Count);
			
			Sectors = _map.MapColors(lData, rData).ToList();
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

		

		#endregion


		private static int FftIndex2Frequency(int index, int length, int sampleRate) {
			return index * sampleRate / length;
		}
	}
}