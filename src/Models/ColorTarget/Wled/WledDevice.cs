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
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Wled {
	public class WledDevice : ColorTarget, IColorTarget, IDisposable {
		private string IpAddress { get; set; }
		private static readonly int port = 21324;
		private readonly HttpClient _httpClient;
		private readonly UdpClient _udpClient;

		private int _brightness;
		private WledData _data;

		private bool _disposed;
		private IPEndPoint? _ep;
		private int _ledCount;
		private int _multiplier;
		private int _offset;
		private StripMode _stripMode;
		private int _targetSector;

		public WledDevice(WledData wd, ColorService colorService) : base(colorService) {
			colorService.ColorSendEvent += SetColor;
			ColorService = colorService;
			colorService.ControlService.RefreshSystemEvent += RefreshSystem;
			_udpClient = ColorService.ControlService.UdpClient;
			_httpClient = ColorService.ControlService.HttpSender;
			_data = wd ?? throw new ArgumentException("Invalid WLED data.");
			Id = _data.Id;
			IpAddress = _data.IpAddress;
			_brightness = _data.Brightness;
			_multiplier = _data.LedMultiplier;
			ReloadData();
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

			Log.Information($"{_data.Tag}::Starting stream: {_data.Id}...");
			_targetSector = _data.TargetSector;
			_ep = IpUtil.Parse(IpAddress, port);
			if (_ep == null) {
				return;
			}

			Streaming = true;
			await FlashColor(Color.Red);
			await UpdateLightState(Streaming);
			Log.Information($"{_data.Tag}::Stream started: {_data.Id}.");
		}


		public async Task FlashColor(Color color) {
			var packet = new List<byte> {ByteUtils.IntByte(2), ByteUtils.IntByte(10)};
			for (var i = 0; i < _data.LedCount; i++) {
				packet.Add(color.R);
				packet.Add(color.G);
				packet.Add(color.B);
			}

			try {
				await _udpClient.SendAsync(packet.ToArray(), packet.Count, _ep).ConfigureAwait(false);
			} catch (Exception e) {
				Log.Debug("Exception, look at that: " + e.Message);
			}
		}


		public async Task StopStream() {
			if (!Streaming) {
				return;
			}


			Streaming = false;
			await FlashColor(Color.Black).ConfigureAwait(false);
			await UpdateLightState(false).ConfigureAwait(false);
			await Task.FromResult(true);
			Log.Information($"{_data.Tag}::Stream stopped: {_data.Id}.");
		}

		
		public void SetColor(Color[] list, Color[] colors1, int arg3, bool force = false) {
			if (!Streaming || !Enable || Testing && !force) {
				return;
			}

			var colors = list;

			if (_stripMode == StripMode.Single) {
				if (_targetSector >= colors1.Length || _targetSector == -1) {
					return;
				}

				colors = ColorUtil.FillArray(colors1[_targetSector], _ledCount);
			} else {
				colors = ColorUtil.TruncateColors(colors, _offset, _ledCount, _multiplier);
				if (_stripMode == StripMode.Loop) {
					colors = ShiftColors(colors);
				} else {
					if (_data.ReverseStrip) {
						colors = colors.Reverse().ToArray();
					}
				}
			}

			var packet = new byte[2 + colors.Length * 3];
			var timeByte = 255;
			packet[0] = ByteUtils.IntByte(2);
			packet[1] = ByteUtils.IntByte(timeByte);
			var pInt = 2;
			foreach (var t in colors) {
				packet[pInt] = t.R;
				packet[pInt + 1] = t.G;
				packet[pInt + 2] = t.B;
				pInt += 3;
			}

			if (_ep == null) {
				Log.Debug("No endpoint.");
				return;
			}

			try {
				_udpClient.SendAsync(packet.ToArray(), packet.Length, _ep).ConfigureAwait(false);
				ColorService?.Counter.Tick(Id);
			} catch (Exception e) {
				Log.Debug("Exception: " + e.Message);
			}
		}


		public Task ReloadData() {
			var oldBrightness = _brightness;
			var dev = DataUtil.GetDevice<WledData>(Id);
			if (dev != null) {
				_data = dev;
			}

			_offset = _data.Offset;
			_brightness = _data.Brightness;
			IpAddress = _data.IpAddress;
			Enable = _data.Enable;
			_stripMode = (StripMode) _data.StripMode;
			_targetSector = _data.TargetSector;
			_multiplier = _data.LedMultiplier;
			if (_multiplier == 0) {
				_multiplier = 1;
			}

			if (oldBrightness != _brightness) {
				Log.Debug($"Brightness has changed!! {oldBrightness} {_brightness}");
				UpdateLightState(Streaming).ConfigureAwait(false);
			}

			_ledCount = _data.LedCount;
			return Task.CompletedTask;
		}


		public void Dispose() {
			Dispose(true).ConfigureAwait(true);
			GC.SuppressFinalize(this);
		}


		public bool IsEnabled() {
			return _data.Enable;
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

		// private async Task<WledStateData?> GetLightState() {
		// 	var url = "http://" + IpAddress + "/json/";
		// 	Log.Debug("URL is " + url);
		// 	var res = await _httpClient.GetAsync(url);
		// 	res.EnsureSuccessStatusCode();
		// 	if (res.Content != null && res.Content.Headers.ContentType?.MediaType == "application/json") {
		// 		var contentStream = await res.Content.ReadAsStreamAsync();
		//
		// 		try {
		// 			return await JsonSerializer.DeserializeAsync<WledStateData>(contentStream,
		// 				new JsonSerializerOptions {IgnoreNullValues = true, PropertyNameCaseInsensitive = true});
		// 		} catch (JsonException) // Invalid JSON
		// 		{
		// 			Console.WriteLine("Invalid JSON.");
		// 		}
		// 	} else {
		// 		Console.WriteLine("HTTP Response was invalid and cannot be de-serialised.");
		// 	}
		//
		// 	return null;
		// }


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