#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Models.ColorSource.UDP;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Wled {
	public class WledDevice : ColorTarget, IColorTarget, IDisposable {
		private string IpAddress { get; set; }
		private const int Port = 21324;
		private readonly HttpClient _httpClient;
		private readonly UdpClient _udpClient;

		private int _brightness;
		private WledData _data;

		private bool _disposed;
		private IPEndPoint? _ep;
		private int _ledCount;
		private float _multiplier;
		private int _offset;
		private StripMode _stripMode;
		private int _targetSector;
		private int _protocol = 2;
		private WledSegment[] _segments;

		public WledDevice(WledData wd, ColorService cs) : base(cs) {
			_segments = Array.Empty<WledSegment>();
			cs.ControlService.RefreshSystemEvent += RefreshSystem;
			_udpClient = cs.ControlService.UdpClient;
			_httpClient = cs.ControlService.HttpSender;
			_data = wd ?? throw new ArgumentException("Invalid WLED data.");
			Id = _data.Id;
			IpAddress = _data.IpAddress;
			_brightness = _data.Brightness;
			_multiplier = _data.LedMultiplier;
			ReloadData();
			cs.ColorSendEventAsync += SetColors;
		}

		public bool Enable { get; set; }
		public bool Streaming { get; set; }
		public bool Testing { get; set; }
		public string Id { get; }

		IColorTargetData IColorTarget.Data {
			get => _data;
			set => _data = (WledData) value;
		}


		public async Task StartStream(CancellationToken ct) {
			if (Streaming || !Enable) {
				return;
			}

			Log.Debug($"{_data.Tag}::Starting stream: {_data.Id}...");
			ColorService.StartCounter++;
			_targetSector = _data.TargetSector;
			_ep = IpUtil.Parse(IpAddress, Port);
			if (_ep == null) {
				return;
			}

			Streaming = true;
			await FlashColor(Color.Black);
			await UpdateLightState(Streaming);
			Log.Debug($"{_data.Tag}::Stream started: {_data.Id}.");
			ColorService.StartCounter--;
		}


		public async Task FlashColor(Color color) {
			try {
				var colors = ColorUtil.FillArray(color, _ledCount);
				var cp = new ColorPacket(colors, (UdpStreamMode) _protocol);
				var packet = cp.Encode();
				await _udpClient.SendAsync(packet.ToArray(), packet.Length, _ep).ConfigureAwait(false);
			} catch (Exception e) {
				Log.Debug("Exception, look at that: " + e.Message);
			}
		}


		public async Task StopStream() {
			if (!Streaming) {
				return;
			}
			Log.Debug($"{_data.Tag}::Stopping stream...{_data.Id}.");
			ColorService.StopCounter++;
			Streaming = false;
			await FlashColor(Color.Black).ConfigureAwait(false);
			await UpdateLightState(false).ConfigureAwait(false);
			await Task.FromResult(true);
			Log.Debug($"{_data.Tag}::Stream stopped: {_data.Id}.");
			ColorService.StopCounter--;
		}


		public Task ReloadData() {
			var oldBrightness = _brightness;
			var dev = DataUtil.GetDevice<WledData>(Id);
			if (dev != null) {
				_data = dev;
			}

			_protocol = _data.Protocol;
			_offset = _data.Offset;
			_brightness = _data.Brightness;
			IpAddress = _data.IpAddress;
			Enable = _data.Enable;
			_stripMode = _data.StripMode;
			_targetSector = _data.TargetSector;
			_multiplier = _data.LedMultiplier;
			if (_multiplier == 0) {
				_multiplier = 1;
			}

			if (oldBrightness != _brightness) {
				Log.Debug($"Brightness has changed!! {oldBrightness} {_brightness}");
				UpdateLightState(Streaming).ConfigureAwait(false);
			}

			_segments = _data.Segments;
			_ledCount = _data.LedCount;
			return Task.CompletedTask;
		}


		public void Dispose() {
			Dispose(true).ConfigureAwait(true);
			GC.SuppressFinalize(this);
		}

		private Task SetColors(object sender, ColorSendEventArgs args) {
			return SetColor(args.LedColors, args.SectorColors, args.Force);
		}


		private async Task SetColor(Color[] list, IReadOnlyList<Color> colors1, bool force = false) {
			if (!Streaming || !Enable || Testing && !force) {
				return;
			}

			var toSend = list;
			if (_stripMode == StripMode.Single) {
				if (_targetSector >= colors1.Count || _targetSector == -1) {
					return;
				}

				toSend = ColorUtil.FillArray(colors1[_targetSector], _ledCount);
			} else if (_stripMode == StripMode.Sectored) {
				var output = new Color[_ledCount];
				foreach (var seg in _segments) {
					var cols = ColorUtil.TruncateColors(toSend, seg.Offset, seg.LedCount, seg.Multiplier);
					if (seg.ReverseStrip) cols = cols.Reverse().ToArray();
					var start = seg.Start;
					foreach (var col in cols) {
						if (start >= _ledCount) {
							Log.Warning($"Error, dest color idx is greater than led count: {start}/{_ledCount}");
							continue;
						}
						output[start] = col;
						start++;
					}
				}
				toSend = output;
			} else {
				toSend = ColorUtil.TruncateColors(toSend, _offset, _ledCount, _multiplier);
				if (_stripMode == StripMode.Loop) {
					toSend = ShiftColors(toSend);
				} else {
					if (_data.ReverseStrip) {
						toSend = toSend.Reverse().ToArray();
					}
				}
			}
			
			if (_ep == null) {
				Log.Debug("No endpoint.");
				return;
			}

			try {
				var cp = new ColorPacket(toSend,(UdpStreamMode) _protocol);
				var packet = cp.Encode(255); 
				await _udpClient.SendAsync(packet.ToArray(), packet.Length, _ep).ConfigureAwait(false);
				ColorService.Counter.Tick(Id);
			} catch (Exception e) {
				Log.Debug("Exception: " + e.Message + " at " + e.StackTrace);
			}
		}


		private Color[] ShiftColors(IReadOnlyList<Color> input) {
			var output = new Color[input.Count];
			var il = output.Length - 1;
			if (!_data.ReverseStrip) {
				for (var i = 0; i < input.Count / 2; i++) {
					output[i] = input[i];
					output[il - i] = input[i];
				}
			} else {
				var l = 0;
				for (var i = (input.Count - 1) / 2; i >= 0; i--) {
					output[i] = input[l];
					output[il - i] = input[l];
					l++;
				}
			}


			return output;
		}

		private void RefreshSystem() {
			ReloadData();
		}

		private async Task UpdateLightState(bool on, int bri = -1) {
			var scaledBright = bri == -1 ? _brightness / 100f * 255f : bri;
			if (scaledBright > 255) {
				scaledBright = 255;
			}

			var url = "http://" + IpAddress + "/win";
			url += "&T=" + (on ? "1" : "0");
			url += "&A=" + (int) scaledBright;
			await _httpClient.GetAsync(url).ConfigureAwait(false);
		}

		protected virtual async Task Dispose(bool disposing) {
			if (_disposed) {
				return;
			}

			if (disposing) {
				if (Streaming) {
					await StopStream();
				}
			}

			_disposed = true;
		}
	}
}