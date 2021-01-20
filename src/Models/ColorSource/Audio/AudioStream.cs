using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV.Saliency;
using Glimmr.Models.LED;
using Glimmr.Models.Util;
using Glimmr.Services;
using ManagedBass;

using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json;
using Q42.HueApi.ColorConverters.HSB;
using rpi_ws281x;
using Serilog;
using Color = System.Drawing.Color;

namespace Glimmr.Models.ColorSource.Audio {
	public sealed class AudioStream : IColorSource, IDisposable {
		
		public bool SourceActive { get; set; }
		public bool SendColors { get; set; }
		private bool _disposed;
		private List<AudioData> _devices;
		private int _recordDeviceIndex;
		private int _channels;
		private int _frequency;
		private float _max;
		private readonly CancellationToken _token;
		private readonly ColorService _cs;
		private SystemData _sd;
		private AudioMap _map;

		public List<Color> Colors { get; private set; }
		public List<Color> Sectors { get; private set; }
		

		public AudioStream(ColorService cs) {
			_cs = cs;
			Bass.Init();
		}

		private void LoadData() {
			Log.Debug("Reloading audio data");
			_sd = DataUtil.GetObject<SystemData>("SystemData");
			Colors = ColorUtil.EmptyList(_sd.LedCount);
			Sectors = ColorUtil.EmptyList(28);

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
					DataUtil.InsertCollection<AudioData>("Dev_Audio", ad);
					_devices.Add(ad);
				} catch (Exception e) {
					Log.Warning("Error loading devices.", e);
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
		
		public async void Initialize(CancellationToken ct) {
			LoadData();
			if (_recordDeviceIndex != -1) {
				Log.Debug("Starting stream with device " + _recordDeviceIndex);
				Bass.RecordInit(_recordDeviceIndex);
				Bass.RecordStart(48000, 5, BassFlags.Float, Update);
				while (!ct.IsCancellationRequested) {
					await Task.Delay(1, CancellationToken.None);
				}

				Log.Debug("Audio stream canceled.");
				Bass.RecordFree();
			} else {
				Log.Debug("No recording device available.");
			}
		}

		
		public void Refresh() {
			LoadData();
		}

		
		private bool Update(int handle, IntPtr buffer, int length, IntPtr user) {
			var samples = 2048 * 5;
			var fft = new float[samples]; // fft data buffer
			// Get our FFT for "everything"
			Bass.ChannelGetData(handle, fft, (int) DataFlags.FFT4096 | (int) DataFlags.FFTIndividual);
			var lData = new Dictionary<int, float>();
			var rData = new Dictionary<int, float>();
			var cData = new Dictionary<int, float>();
			var realIndex = 0;
			var iCount = 0;
			
			for (var a = 0; a < samples; a++) {
				var val = FlattenValue(fft[a]);
				var freq = FftIndex2Frequency(realIndex, 4096, 48000);
				if (val <= .01) {
					continue;
				}

				switch (iCount) {
					case 0:
						lData[freq] = val;
						break;
					case 1:
						rData[freq] = val;
						break;
					case 2:
						cData[freq] = val;
						break;
				}

				iCount++;
				
				if (iCount < 5) {
					continue;
				}

				iCount = 0;
				realIndex++;
			}
			

			//Log.Debug("LDATA: " + JsonConvert.SerializeObject(lData));

			var lAmps = SortChannels(lData);
			var rAmps = SortChannels(rData);
			Sectors = ColorUtil.EmptyList(Sectors.Count);
			Colors = ColorUtil.EmptyList(Colors.Count);
			SourceActive = lAmps.Count != 0 || rAmps.Count != 0;
			Sectors = _map.MapColors(lAmps, rAmps, 28).ToList();
			Colors = ColorUtil.SectorsToleds(Sectors.ToList(), _sd);
			if (SendColors) {
				_cs.SendColors(Colors, Sectors);
			}
			return true;
		}

		private float FlattenValue(float input) {
			input *= 2;
			// Drop anything that's not at least .01
			if (input < .075) {
				return 0;
			}
			
			input += .55f;
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
				var freq2 = new KeyValuePair<int,float>();
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
				cValues[step] = new KeyValuePair<float, float>(sum, stepMax);
			}
			return cValues;
		}


		private void ConsoleView(IReadOnlyList<float> input) {
			Console.Clear();
			Console.SetCursorPosition(0, 0);
			LogColor(input[6], "|");
			LogColor(input[5], "|");
			LogColor(input[4], "|");
			LogColor(input[3], "|");
			LogColor(input[3]);

			Console.WriteLine();
			LogColor(input[7], "|");
			Console.ForegroundColor = ConsoleColor.White;
			Console.Write($@"    {_max:F2}    |");
			LogColor(input[2]);
			Console.WriteLine();
			LogColor(input[8], "|");
			LogColor(input[9], "|");
			LogColor(input[10], "|");
			LogColor(input[11], "|");
			LogColor(input[0]);
			Console.WriteLine();
		}

		private static float HueFromAmplitude(float input) {
			var point = input / 24 * 360;
			if (point > 360) point = 360;
			//if (input == 0) point = 0;
			return point;
		}

		private void LogColor(float amplitude, string separator = "") {
			var hue = HueFromAmplitude(amplitude);
			var value = amplitude > 0 ? 1 : 0;
			Console.ForegroundColor = ColorFromSystem(ColorUtil.HsvToColor(hue, 1, value));
			Console.Write($@"{amplitude:F2}{separator}");
		}

		#endregion


		private static int FftIndex2Frequency(int index, int length, int sampleRate) {
			return index * sampleRate / length;
		}


		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}


		private static ConsoleColor ColorFromSystem(Color input) {
			if (input.R > (input.G + input.B) / 2) return ConsoleColor.Red;

			if (input.G > (input.R + input.B) / 2) return ConsoleColor.Green;

			if (input.B > (input.R + input.G) / 2) return ConsoleColor.Blue;

			return ConsoleColor.White;
		}

		private void Dispose(bool disposing) {
			if (_disposed) return;

			if (disposing) {
				//ac?.Dispose();
			}

			_disposed = true;
		}

		
	}
}