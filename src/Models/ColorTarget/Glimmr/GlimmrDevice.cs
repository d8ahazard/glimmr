#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Glimmr {
	public class GlimmrDevice : ColorTarget, IColorTarget, IDisposable {
		private GlimmrData _data;
		private GlimmrData _sourceData;
		private const int Port = 8889;
		private readonly HttpClient _httpClient;
		private readonly UdpClient _udpClient;
		private readonly List<Color> _updateColors;
		private bool _disposed;
		private SystemData _sd;
		private IPEndPoint? _ep;
		public bool Enable { get; set; }
		public bool Streaming { get; set; }
		public bool Testing { get; set; }
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; } = "Glimmr";


		IColorTargetData IColorTarget.Data {
			get => _data;
			set => _data = (GlimmrData) value;
		}

		public GlimmrDevice(GlimmrData wd, ColorService colorService) : base(colorService) {
			colorService.ColorSendEvent += SetColor;
			_udpClient = colorService.ControlService.UdpClient;
			_httpClient = colorService.ControlService.HttpSender;
			_updateColors = new List<Color>();
			_data = wd ?? throw new ArgumentException("Invalid Glimmr data.");
			Id = _data.Id;
			Enable = _data.Enable;
			IpAddress = _data.IpAddress;
			_sd = DataUtil.GetSystemData();
			colorService.ControlService.RefreshSystemEvent += RefreshSystem;
		}

		private void RefreshSystem() {
			_sd = DataUtil.GetSystemData();
		}


		public async Task StartStream(CancellationToken ct) {
			if (Streaming || !Enable) {
				return;
			}

			Log.Information($"{_data.Tag}::Starting stream: {_data.Id}...");
			var sd = DataUtil.GetSystemData();
			var glimmrData = new GlimmrData(sd);
			await SendPost("startStream", JsonConvert.SerializeObject(glimmrData)).ConfigureAwait(false);
			_ep = IpUtil.Parse(IpAddress, Port);
			Streaming = true;
			Log.Information($"{_data.Tag}::Stream started: {_data.Id}.");
		}


		public async Task FlashColor(Color color) {
			var packet = new List<byte>();
			// Set mode to D-RGB, dude.
			const int timeByte = 255;
			packet.Add(ByteUtils.IntByte(2));
			packet.Add(ByteUtils.IntByte(timeByte));
			for (var i = 0; i < _data.LedCount; i++) {
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


		public async Task StopStream() {
			if (!Streaming) {
				return;
			}

			await FlashColor(Color.FromArgb(0, 0, 0));
			Streaming = false;
			await SendPost("mode", 0.ToString());
			Log.Information($"{_data.Tag}::Stream stopped: {_data.Id}.");
		}


		public void SetColor(List<Color> leds, List<Color> sectors, int arg3, bool force = false) {
			if (!Streaming || !Enable || Testing && !force) {
				return;
			}

			if (_ep == null) {
				Log.Debug("No endpoint.");
				return;
			}

			if (_data.MirrorHorizontal) {
				var left = new Color[_sd.LeftCount];
				var right = new Color[_sd.RightCount];
				var top = new Color[_sd.TopCount];
				var bottom = new Color[_sd.BottomCount];
				for (var i = 0; i < right.Length; i++) {
					right[i] = leds[i];
				}

				var ct = 0;
				for (var i = 0; i < top.Length; i++) {
					var tIdx = right.Length + i;
					top[ct] = leds[tIdx];
					ct++;
				}
				
				ct = 0;
				for (var i = 0; i < left.Length; i++) {
					var lIdx = right.Length + top.Length + i;
					left[ct] = leds[lIdx];
					ct++;
				}
				ct = 0;
				for (var i = 0; i < bottom.Length; i++) {
					var lIdx = left.Length + right.Length + top.Length + i;
					bottom[ct] = leds[lIdx];
					ct++;
				}

				leds = new List<Color>();
				left = left.Reverse().ToArray();
				top = top.Reverse().ToArray();
				bottom = bottom.Reverse().ToArray();
				right = right.Reverse().ToArray();
				
				leds.AddRange(left);
				leds.AddRange(top);
				leds.AddRange(right);
				leds.AddRange(bottom);
			}

			var packet = new List<byte> {ByteUtils.IntByte(2), ByteUtils.IntByte(255)};
			foreach (var color in leds) {
				packet.Add(ByteUtils.IntByte(color.R));
				packet.Add(ByteUtils.IntByte(color.G));
				packet.Add(ByteUtils.IntByte(color.B));
			}

			try {
				_udpClient.SendAsync(packet.ToArray(), packet.Count, _ep).ConfigureAwait(false);
				ColorService?.Counter.Tick(Id);
			} catch (Exception e) {
				Log.Debug("Exception: " + e.Message);
			}
		}


		public Task ReloadData() {
			var id = _data.Id;
			_sd = DataUtil.GetSystemData();
			var dev = DataUtil.GetDevice<GlimmrData>(id);
			if (dev == null) return Task.CompletedTask;
			_data = dev;
			IpAddress = _data.IpAddress;
			Enable = _data.Enable;
			Log.Debug($"Reloaded LED Data for {id}: " + JsonConvert.SerializeObject(_data));
			return Task.CompletedTask;
		}


		public void Dispose() {
			Dispose(true).ConfigureAwait(true);
			GC.SuppressFinalize(this);
		}


		public bool IsEnabled() {
			return _data.Enable;
		}


		public async Task UpdatePixel(int pixelIndex, Color color) {
			if (_updateColors.Count == 0) {
				for (var i = 0; i < _data.LedCount; i++) {
					_updateColors.Add(Color.FromArgb(0, 0, 0, 0));
				}
			}

			if (pixelIndex >= _data.LedCount) {
				return;
			}

			_updateColors[pixelIndex] = color;
			SetColor(_updateColors, _updateColors, 0);
			await Task.FromResult(true);
		}


		private async Task SendPost(string target, string value) {
			Uri uri;
			if (string.IsNullOrEmpty(IpAddress) && !string.IsNullOrEmpty(Id)) {
				IpAddress = Id;
				_data.IpAddress = Id;
			}

			try {
				uri = new Uri("http://" + IpAddress + "/api/DreamData/" + target);
				//Log.Debug($"Posting to {uri}: " + value);
			} catch (UriFormatException e) {
				Log.Warning("URI Format exception: " + e.Message);
				return;
			}


			var stringContent = new StringContent(value, Encoding.UTF8, "application/json");
			try {
				var res = await _httpClient.PostAsync(uri, stringContent);
				//Log.Debug("Response: " + res.StatusCode);
			} catch (Exception e) {
				Log.Warning("HTTP Request Exception: " + e.Message);
			}

			stringContent.Dispose();
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