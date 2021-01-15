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
using Newtonsoft.Json;
using Q42.HueApi.ColorConverters.HSB;
using Serilog;
using Color = System.Drawing.Color;

namespace Glimmr.Models.ColorSource.Audio {
	public sealed class AudioStream : IColorSource, IDisposable {
		
		public bool Streaming { get; set; }
		public bool SourceActive { get; set; }
		
		private bool _disposed;
		private List<AudioData> _devices;
		private int _recordDeviceIndex;
		private int _channels;
		private int _frequency;
		private readonly int _sensitivity;
		private float _max;
		private readonly CancellationToken _token;
		private readonly List<Color> _colors;
		private readonly ColorService _cs;
		private LedData _ledData;
		private AudioMap _map;

		public AudioStream(ColorService cs, CancellationToken cancellationToken) {
			_cs = cs;
			_token = cancellationToken;
			_colors = new List<Color>();
			_ledData = DataUtil.GetObject<LedData>("LedData");
			for (var i = 0; i < 28; i++) _colors.Add(Color.Black);
			
			_devices = new List<AudioData>();
			_map = new AudioMap(0);
			_recordDeviceIndex = -1;
			_sensitivity = DataUtil.GetItem("Sensitivity") ?? 5;
			LoadDevices();
		}

		public void UpdateLedData() {
			_ledData = DataUtil.GetObject<LedData>("LedData");
		}

		private static float Limit(float value, int inclusiveMinimum, int inclusiveMaximum) {
			if (value < inclusiveMinimum) return inclusiveMinimum;
			if (value > inclusiveMaximum) return inclusiveMaximum;
			return value;
		}


		private void LoadDevices() {
			Bass.Init();
			_devices = new List<AudioData>();
			string rd = DataUtil.GetItem("RecDev");
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
		}

		public async void Initialize() {
			if (_recordDeviceIndex != -1) {
				
				while (!_token.IsCancellationRequested) {
					await Task.Delay(1, CancellationToken.None);
				}
				Log.Debug("Audio stream canceled.");
			} else {
				Log.Debug("No recording device available.");
			}
		}

		public void Refresh() {
		}

		
		

		public void ToggleSend(bool enable = true) {
			Streaming = enable;
			if (!enable) return;
			Log.Debug("Starting stream with device " + _recordDeviceIndex);
			Bass.RecordInit(_recordDeviceIndex);
			var info = Bass.RecordingInfo;
			Log.Debug("Recording info: " + JsonConvert.SerializeObject(info));
			_channels = 2;
			for (var i = 0; i < _channels; i++) {
				Bass.ChannelGetInfo(i, out var cInfo);
				Log.Debug("Channel Info: " + JsonConvert.SerializeObject(cInfo));
			}

			_frequency = info.Frequency == 0 ? 48000 : info.Frequency;
			Bass.RecordStart(_frequency, _channels, BassFlags.Float, Update);
		}

		private bool Update(int handle, IntPtr buffer, int length, IntPtr user) {
			if (!Streaming) return true;
			if (_token.IsCancellationRequested) {
				Log.Debug("We dun canceled our token.");
				Bass.Free();
			}

			var boost = 2;
			var samples = 1024;
			var fft = new float[samples]; // fft data buffer
			// Get our FFT for "everything"
			var channelGetData = Bass.ChannelGetData(handle, fft, (int) DataFlags.FFT1024 | (int) DataFlags.FFTIndividual);
			var lData = new Dictionary<int, float>();
			var rData = new Dictionary<int, float>();
			var sa = false;
			// LeftL
			var realIndex = 1;
			for (var a = 0; a < samples; a+= 2) {
				var val = fft[a] * boost;
				val = FlattenValue(val);
				var amp = val;
				var freq = FftIndex2Frequency(realIndex, samples / 2, _frequency);
				if (amp > _max) _max = amp;
				lData[freq] = amp;
				realIndex++;
			}

			realIndex = 1;
			for (var a = 1; a < samples; a+=2) {
				var val = fft[a] * boost;
				val = FlattenValue(val);
				var amp = val;
				var freq = FftIndex2Frequency(realIndex, samples / 2, _frequency);
				if (amp > _max) _max = amp;
				rData[freq] = amp;
				realIndex++;
			}

			
			var lAmps = SortChannels(lData);
			var rAmps = SortChannels(rData);

			SourceActive = lAmps.Count != 0 || rAmps.Count != 0;

			var colors = _map.MapColors(lAmps, rAmps, 28);
			
			var ledCols = ColorUtil.SectorsToleds(colors.ToList(), _ledData);
			//Log.Debug("Colors: " + JsonConvert.SerializeObject(ledCols));

			_cs.SendColors(ledCols, colors.ToList());
			return true;
		}

		private float FlattenValue(float input) {
			if (input < .001) {
				return 0;
			}

			if (input < .1) {
				return .3f;
			}
			
			if (input < .3) {
				return .5f;
			}

			if (input < .7) {
				return .75f;
			}
			
			if (input < .75) {
				return 1f;
			}
			
			return 1;
		}

		#region intColors

		#endregion

		#region floatColors

		private Dictionary<int, KeyValuePair<float, float>> SortChannels(Dictionary<int, float> cData) {
			
			var cValues = new Dictionary<int, KeyValuePair<float,float>>();
			var steps = new[] {30, 60, 125, 250, 500, 1000, 2000};
			foreach (var step in steps) {
				var stepMax = 0;
				foreach (var (frequency, amplitude) in cData) {
					if (frequency >= step && frequency < step * 2 && amplitude > stepMax) {
						var avg = (frequency - step) / step;
						cValues[step] = new KeyValuePair<float, float>(avg, amplitude);
					}
				}
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
			Console.ForegroundColor = ColorFromSystem(ColorUtil.ColorFromHsv(hue, 1, value));
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