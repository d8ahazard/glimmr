using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using LifxNet;
using ManagedBass;
using Newtonsoft.Json;
using Color = System.Drawing.Color;

namespace Glimmr.Models.CaptureSource.Audio {
    public sealed class AudioStream : IDisposable {
        private bool _disposed;
        private List<AudioData> _devices;
        private int _recordDeviceIndex;
        private int _channels;
        private int _frequency;
        private int _sensitivity;
        private float _max;
        private CancellationToken _token;
        private List<Color> _colors;
        private bool _sendColors;
        private ColorService _cs;


        public AudioStream(ColorService cs, CancellationToken cancellationToken) {
            _cs = cs;
            _token = cancellationToken;
            _colors = new List<Color>();
            for (var i = 0; i < 12; i++) {
                _colors.Add(Color.Black);
            }

            _devices = new List<AudioData>();
            _recordDeviceIndex = -1;
            _sensitivity = DataUtil.GetItem("Sensitivity") ?? 5;
            LoadDevices();
        }

        public List<Color> GetColors() {
            return _colors;
        }
        
        private static float Limit(float value, int inclusiveMinimum, int inclusiveMaximum)
        {
            if (value < inclusiveMinimum) { return inclusiveMinimum; }
            if (value > inclusiveMaximum) { return inclusiveMaximum; }
            return value;
        }


        private void LoadDevices() {
            Bass.Init();
            _devices = new List<AudioData>();
            string rd = DataUtil.GetItem("RecDev");
            for (var a = 0; Bass.RecordGetDeviceInfo(a, out var info); a++) {
                LogUtil.Write("Bass device?" + JsonConvert.SerializeObject(info));
                if (!info.IsEnabled) continue;
                try {
                    var ad = new AudioData();
                    ad.ParseDevice(info);
                    DataUtil.InsertCollection<AudioData>("Dev_Audio", ad);
                    _devices.Add(ad);
                } catch (Exception) {
                
                }
                if (rd == null && a == 0) {
                    DataUtil.SetItem("RecDev", info.Name);
                    rd = info.Name;
                } else {
                    if (rd != info.Name) continue;
                    LogUtil.Write($"Selecting recording device index {a}: {info.Name}");
                    _recordDeviceIndex = a;
                }
            }

            
        }

        public void StartCapture() {
            if (_recordDeviceIndex != -1) {
                SetCapVars();
                
                while (!_token.IsCancellationRequested) {
                    
                }
                
            } else {
                LogUtil.Write("No recording device available.");
            }
        }

        private void SetCapVars() {
            LogUtil.Write("Starting stream with device " + _recordDeviceIndex);
            Bass.Init();
            // Initialize Recording device.
            Bass.RecordInit(1);
            Bass.CurrentRecordingDevice = 1;
            var info = Bass.RecordingInfo;
            LogUtil.Write("Info: " + JsonConvert.SerializeObject(info));
            _channels = info.Channels == 0 ? 2 : info.Channels;
            _frequency = info.Frequency == 0 ? 48000 : info.Frequency;
            LogUtil.Write($"Setting channels and frequency to {_channels} and {_frequency}.");
            var record = Bass.RecordStart(_frequency, _channels, BassFlags.Float, Update);
            var error = Bass.LastError;
            LogUtil.Write("err? " + error);
        }

        public void ToggleSend(bool enable = true) {
            _sendColors = enable;
        }

        private bool Update(int handle, IntPtr buffer, int length, IntPtr user) {
            if (!_sendColors) return true;
            if (_token.IsCancellationRequested) {
                LogUtil.Write("We dun canceled our token.");
                Bass.Free();
            }

            var samples = 256;
            var fft = new float[samples]; // fft data buffer
            var fftStereo = new float[3]; // fft data buffer
            // Get our FFT for "everything"
            var channelGetData = Bass.ChannelGetData(handle, fft, (int) DataFlags.FFT256);
            //LogUtil.Write($"FFT {channelGetData}: " + JsonConvert.SerializeObject(fft));
            var cData = new Dictionary<int, float>();
            for (var a = 0; a < samples; a++) {
                var val = fft[a];
                if (val > 0.01) {
                    var amp = val * 100;
                    var freq = FftIndex2Frequency(a, samples, _frequency);
                    if (amp > _max) _max = amp;
                    cData[freq] = amp;
                }
            }

            // Now get them for the stereo left/right
            channelGetData = Bass.ChannelGetData(handle, fftStereo, (int) DataFlags.FFTIndividual);
            var amps = SortChannels(cData, fftStereo);
            for (var q = 0; q < amps.Length; q++) {
                var amp = amps[q];
                var value = amp > 0 ? 1 : 0;
                _colors[q] = ColorUtil.ColorFromHsv(HueFromAmplitude(amp), 1, value);
            }
            LogUtil.Write("Sending something...");
            _cs.SendColors(_colors.ToList(),_colors.ToList(),_colors.ToList());
            return true;
        }

        #region intColors

        #endregion

        #region floatColors

        private float[] SortChannels(Dictionary<int, float> cData, float[] stereo) {
            //     Sub-bass	20 to 60 Hz
            //     Bass	60 to 250 Hz
            //     Low midrange	250 to 500 Hz
            //     Midrange	500 Hz to 2 kHz
            //     Upper midrange	2 to 4 kHz
            //     Presence	4 to 6 kHz
            //     Brilliance	6 to 20 kHz

            var subRange = new List<float>();
            var bassRange = new List<float>();
            var lowMidRange = new List<float>();
            var midRange = new List<float>();
            var highMidRange = new List<float>();
            var highRange = new List<float>();

            var cValues = new Dictionary<string, float>();

            foreach (var val in cData) {
                switch (0) {
                    case 0 when val.Key < 60:
                        subRange.Add(val.Value);
                        break;
                    case 0 when val.Key > 60 && val.Key <= 350:
                        bassRange.Add(val.Value);
                        break;
                    case 0 when val.Key > 350 && val.Key <= 2000:
                        lowMidRange.Add(val.Value);
                        break;
                    case 0 when val.Key > 2000 && val.Key <= 8000:
                        highMidRange.Add(val.Value);
                        break;
                    case 0 when val.Key > 8000:
                        highRange.Add(val.Value);
                        break;
                }
            }

            cValues["sub"] = subRange.Count > 0 ? (int) subRange.Average() : 0;
            cValues["bass"] = bassRange.Count > 0 ? (int) bassRange.Average() : 000;
            cValues["lmid"] = lowMidRange.Count > 0 ? (int) lowMidRange.Average() : 000;
            cValues["hmid"] = highMidRange.Count > 0 ? (int) highMidRange.Average() : 000;
            cValues["high"] = highRange.Count > 0 ? (int) highRange.Average() : 000;
            cValues["l"] = stereo[0] * 100;
            cValues["c"] = stereo[1] * 100;
            cValues["r"] = stereo[2] * 100;
            var sectors = new float[12];
            sectors[0] = cValues["bass"] + cValues["r"];
            sectors[1] = cValues["lmid"] + cValues["r"];
            sectors[2] = cValues["hmid"] + cValues["r"];
            sectors[3] = cValues["hmid"] + cValues["c"];
            sectors[4] = cValues["high"];
            sectors[5] = cValues["hmid"] + cValues["c"];
            sectors[6] = cValues["hmid"] + cValues["l"];
            sectors[7] = cValues["lmid"] + cValues["l"];
            sectors[8] = cValues["bass"] + cValues["l"];
            sectors[9] = cValues["bass"] + cValues["c"];
            sectors[10] = cValues["sub"];
            sectors[11] = cValues["bass"] + cValues["r"];
            
            //LogUtil.Write("Cvalues: " + JsonConvert.SerializeObject(cValues));
            var output = new float[12];
            for(var l = 0; l < 12; l++) {
                var v = sectors[l] - _sensitivity;
                v = Limit(v, 0, 60);
                output[l] = v;
            }
            //ConsoleView(output);
            return output;
        }


        private void ConsoleView(float[] input) {
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
            if (input.R > (input.G + input.B) / 2) {
                return ConsoleColor.Red;
            }

            if (input.G > (input.R + input.B) / 2) {
                return ConsoleColor.Green;
            }

            if (input.B > (input.R + input.G) / 2) {
                return ConsoleColor.Blue;
            }

            return ConsoleColor.White;
        }

        private void Dispose(bool disposing) {
            if (_disposed) {
                return;
            }

            if (disposing) {
                //ac?.Dispose();
            }

            _disposed = true;
        }
    }
}