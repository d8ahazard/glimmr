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
using Newtonsoft.Json;
using Serilog;

namespace Glimmr.Models.ColorTarget.Wled {
	public class WledDevice : ColorTarget, IColorTarget, IDisposable {
		public WledData Data { get; set; }
		private static readonly int port = 21324;
		private readonly HttpClient _httpClient;
		private readonly UdpClient _udpClient;
		private readonly List<Color> _updateColors;
		private CaptureMode _captureMode;

		private bool _disposed;
		private IPEndPoint _ep;
		private int _ledCount;
		private int _offset;
		private int _sectorCount;
		private StripMode _stripMode;
		private int _targetSector;

		public WledDevice(WledData wd, ColorService colorService) : base(colorService) {
			colorService.ColorSendEvent += SetColor;
			colorService.ControlService.RefreshSystemEvent += RefreshSystem;
			_udpClient = ColorService.ControlService.UdpClient;
			_httpClient = ColorService.ControlService.HttpSender;
			_updateColors = new List<Color>();
			Data = wd ?? throw new ArgumentException("Invalid WLED data.");
			Id = Data.Id;
			Brightness = Data.Brightness;
			ReloadData();
		}

		public bool Enable { get; set; }
		public bool Streaming { get; set; }
		public bool Testing { get; set; }
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; }

		private bool _wasOn;
		private int _lastBri;

		IColorTargetData IColorTarget.Data {
			get => Data;
			set => Data = (WledData) value;
		}


		public async Task StartStream(CancellationToken ct) {
			if (Streaming || !Enable) {
				return;
			}

			Log.Information($"{Data.Tag}::Starting stream: {Data.Id}...");
			_targetSector = ColorUtil.CheckDsSectors(Data.TargetSector);
			_ep = IpUtil.Parse(IpAddress, port);
			Streaming = true;
			await FlashColor(Color.Red);
			await UpdateLightState(Streaming);
			Log.Information($"{Data.Tag}::Stream started: {Data.Id}.");
		}


		public async Task FlashColor(Color color) {
			var packet = new List<byte> {ByteUtils.IntByte(2), ByteUtils.IntByte(10)};
			for (var i = 0; i < Data.LedCount; i++) {
				packet.Add(color.R);
				packet.Add(color.G);
				packet.Add(color.B);
			}

			try {
				if (_udpClient != null) {
					await _udpClient.SendAsync(packet.ToArray(), packet.Count, _ep).ConfigureAwait(false);
				}
			} catch (Exception e) {
				Log.Debug("Exception, look at that: " + e.Message);
			}
		}


		public bool IsEnabled() {
			return Data.Enable;
		}


		public async Task StopStream() {
			if (!Streaming) {
				return;
			}
		

			Streaming = false;
			FlashColor(Color.Black).ConfigureAwait(false);
			UpdateLightState(_wasOn, _lastBri).ConfigureAwait(false);
			await Task.FromResult(true);
			Log.Information($"{Data.Tag}::Stream stopped: {Data.Id}.");
		}


		public void SetColor(List<Color> list, List<Color> colors1, int arg3, bool force = false) {
			if (!Streaming || !Enable || Testing && !force) {
				return;
			}

			var colors = list;

			if (_stripMode == StripMode.Single) {
				if (_targetSector >= colors1.Count || _targetSector == -1) {
					return;
				}

				colors = ColorUtil.FillArray(colors1[_targetSector], _ledCount).ToList();
			} else {
				colors = ColorUtil.TruncateColors(colors, _offset, _ledCount).ToList();
				if (_stripMode == StripMode.Loop) {
					colors = ShiftColors(colors);
				} else {
					if (Data.ReverseStrip) {
						colors.Reverse();
					}
				}
			}

			var packet = new byte[2 + colors.Count * 3];
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
				ColorService.Counter.Tick(Id);
			} catch (Exception e) {
				Log.Debug("Exception: " + e.Message);
			}
		}

		public Task ReloadData() {
			var sd = DataUtil.GetSystemData();
			_captureMode = (CaptureMode) sd.CaptureMode;
			_sectorCount = sd.SectorCount;
			var oldBrightness = Brightness;
			Data = DataUtil.GetDevice<WledData>(Id);
			_offset = Data.Offset;
			Brightness = Data.Brightness;
			IpAddress = Data.IpAddress;
			Enable = Data.Enable;
			_stripMode = (StripMode) Data.StripMode;
			_targetSector = ColorUtil.CheckDsSectors(Data.TargetSector);

			if (oldBrightness != Brightness) {
				Log.Debug($"Brightness has changed!! {oldBrightness} {Brightness}");
				UpdateLightState(Streaming).ConfigureAwait(false);
			}

			_ledCount = Data.LedCount;
			return Task.CompletedTask;
		}


		public void Dispose() {
			Dispose(true).ConfigureAwait(true);
			GC.SuppressFinalize(this);
		}


		private List<Color> ShiftColors(IReadOnlyList<Color> input) {
			var output = new Color[input.Count];
			var il = output.Length - 1;
			if (!Data.ReverseStrip) {
				for (var i = 0; i < input.Count; i++) {
					output[i] = input[i];
					output[il - i] = input[i];
				}
			} else {
				var l = 0;
				for (var i = input.Count - 1; i >= 0; i--) {
					output[i] = input[l];
					output[il - i] = input[l];
					l++;
				}
			}


			return output.ToList();
		}

		public async Task UpdatePixel(int pixelIndex, Color color) {
			if (_updateColors.Count == 0) {
				for (var i = 0; i < Data.LedCount; i++) {
					_updateColors.Add(Color.FromArgb(0, 0, 0, 0));
				}
			}

			if (pixelIndex >= Data.LedCount) {
				return;
			}

			_updateColors[pixelIndex] = color;
			SetColor(_updateColors, null, 0);
			await Task.FromResult(true);
		}

		public void RefreshSystem() {
			ReloadData();
		}

		private async Task UpdateLightState(bool on, int bri = -1) {
			var scaledBright = bri == -1 ? Brightness / 100f * 255f : bri;
			if (scaledBright > 255) scaledBright = 255;
			var url = "http://" + IpAddress + "/win";
			url += "&T=" + (on ? "1" : "0");
			url += "&A=" + (int) scaledBright;
			Log.Debug("LightstateUrl: " + url);
			await _httpClient.GetAsync(url);
		}
		
		private async Task<WledStateData?> GetLightState() {
			var url = "http://" + IpAddress + "/json/";
			Log.Debug("URL is " + url);
			var res = await _httpClient.GetAsync(url);
			res.EnsureSuccessStatusCode();
			if (res.Content is object && res.Content.Headers.ContentType.MediaType == "application/json")
			{
				var contentStream = await res.Content.ReadAsStreamAsync();

				try
				{
					return await System.Text.Json.JsonSerializer.DeserializeAsync<WledStateData>(contentStream, new System.Text.Json.JsonSerializerOptions { IgnoreNullValues = true, PropertyNameCaseInsensitive = true });
				}
				catch (JsonException) // Invalid JSON
				{
					Console.WriteLine("Invalid JSON.");
				}                
			}
			else
			{
				Console.WriteLine("HTTP Response was invalid and cannot be deserialised.");
			}

			return null;
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