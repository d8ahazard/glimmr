using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Serilog;

namespace Glimmr.Models.ColorTarget.Glimmr {
	public class GlimmrDevice : ColorTarget, IColorTarget, IDisposable {
		public GlimmrData Data { get; set; }
		private static readonly int port = 8889;
		private readonly HttpClient _httpClient;
		private readonly UdpClient _udpClient;
		private readonly List<Color> _updateColors;
		private bool _disposed;
		private IPEndPoint _ep;
		private int _len;
		private int _offset;

		public GlimmrDevice(GlimmrData wd, ColorService colorService) : base(colorService) {
			ColorService.ColorSendEvent += SetColor;
			_udpClient = ColorService.ControlService.UdpClient;
			_httpClient = ColorService.ControlService.HttpSender;
			_updateColors = new List<Color>();
			Data = wd ?? throw new ArgumentException("Invalid Glimmr data.");
			Id = Data.Id;
			Enable = Data.Enable;
			IpAddress = Data.IpAddress;
			_len = Data.LedCount;
		}

		public bool Enable { get; set; }
		public bool Streaming { get; set; }
		public bool Testing { get; set; }
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; }


		IColorTargetData IColorTarget.Data {
			get => Data;
			set => Data = (GlimmrData) value;
		}


		public async Task StartStream(CancellationToken ct) {
			if (Streaming || !Enable) {
				return;
			}

			Log.Information($"{Data.Tag}::Starting stream: {Data.Id}...");
			await SendPost("mode", 5).ConfigureAwait(false);
			_ep = IpUtil.Parse(IpAddress, port);
			Streaming = true;
			Log.Information($"{Data.Tag}::Stream started: {Data.Id}.");
		}


		public async Task FlashColor(Color color) {
			var packet = new List<byte>();
			// Set mode to DRGB, dude.
			var timeByte = 255;
			packet.Add(ByteUtils.IntByte(2));
			packet.Add(ByteUtils.IntByte(timeByte));
			for (var i = 0; i < Data.LedCount; i++) {
				packet.Add(color.R);
				packet.Add(color.G);
				packet.Add(color.B);
			}

			try {
				if (_udpClient != null) {
					await _udpClient.SendAsync(packet.ToArray(), packet.Count, _ep);
				}
			} catch (Exception e) {
				Log.Debug("Exception, look at that: " + e.Message);
			}
		}


		public bool IsEnabled() {
			return Data.Enable;
		}


		public async Task StopStream() {
			if (!Enable) {
				return;
			}

			await FlashColor(Color.FromArgb(0, 0, 0));
			Streaming = false;
			await SendPost("mode", 0);
			Log.Information($"{Data.Tag}::Stream stopped: {Data.Id}.");
		}


		public void SetColor(List<Color> leds, List<Color> sectors, int arg3, bool force = false) {
			if (!Streaming || !Enable || Testing && !force) {
				return;
			}

			if (_ep == null) {
				Log.Debug("No endpoint.");
				return;
			}

			var packet = new List<byte>();
			packet.Add(ByteUtils.IntByte(2));
			packet.Add(ByteUtils.IntByte(255));
			foreach (var color in leds) {
				packet.Add(ByteUtils.IntByte(color.R));
				packet.Add(ByteUtils.IntByte(color.G));
				packet.Add(ByteUtils.IntByte(color.B));
			}

			foreach (var color in sectors) {
				packet.Add(ByteUtils.IntByte(color.R));
				packet.Add(ByteUtils.IntByte(color.G));
				packet.Add(ByteUtils.IntByte(color.B));
			}

			try {
				_udpClient.SendAsync(packet.ToArray(), packet.Count, _ep).ConfigureAwait(false);
				ColorService.Counter.Tick(Id);
			} catch (Exception e) {
				Log.Debug("Exception: " + e.Message);
			}
		}


		public Task ReloadData() {
			var id = Data.Id;
			Data = DataUtil.GetDevice<GlimmrData>(id);
			_len = Data.LedCount;
			IpAddress = Data.IpAddress;
			Enable = Data.Enable;
			Log.Debug($"Reloaded LED Data for {id}: " + JsonConvert.SerializeObject(Data));
			return Task.CompletedTask;
		}


		public void Dispose() {
			Dispose(true).ConfigureAwait(true);
			GC.SuppressFinalize(this);
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


		private async Task SendPost(string target, int value) {
			Uri uri;
			if (string.IsNullOrEmpty(IpAddress) && !string.IsNullOrEmpty(Id)) {
				IpAddress = Id;
				Data.IpAddress = Id;
			}

			try {
				uri = new Uri("http://" + IpAddress + "/api/DreamData/" + target);
				Log.Debug($"Posting to {uri}");
			} catch (UriFormatException e) {
				Log.Warning("URI Format exception: " + e.Message);
				return;
			}

			var httpContent = new StringContent(value.ToString());
			httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
			try {
				await _httpClient.PostAsync(uri, httpContent);
			} catch (Exception e) {
				Log.Warning("HTTP Request Exception: " + e.Message);
			}

			httpContent.Dispose();
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