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
		private const int Port = 8889;
		private readonly HttpClient _httpClient;
		private readonly UdpClient _udpClient;
		private GlimmrData _data;
		private bool _disposed;
		private IPEndPoint? _ep;
		private string _ipAddress;
		private SystemData _sd;

		public GlimmrDevice(GlimmrData wd, ColorService cs) : base(cs) {
			_udpClient = cs.ControlService.UdpClient;
			_httpClient = cs.ControlService.HttpSender;
			_data = wd ?? throw new ArgumentException("Invalid Glimmr data.");
			Id = _data.Id;

			Enable = _data.Enable;
			_ipAddress = _data.IpAddress;
			_sd = DataUtil.GetSystemData();
			cs.ControlService.RefreshSystemEvent += RefreshSystem;
			cs.ColorSendEventAsync += SetColors;
		}

		public bool Enable { get; set; }
		public bool Streaming { get; set; }
		public bool Testing { get; set; }
		public string Id { get; }


		IColorTargetData IColorTarget.Data {
			get => _data;
			set => _data = (GlimmrData) value;
		}


		public async Task StartStream(CancellationToken ct) {
			if (Streaming || !Enable) {
				return;
			}

			Log.Debug($"{_data.Tag}::Starting stream: {_data.Id}...");
			var sd = DataUtil.GetSystemData();
			var glimmrData = new GlimmrData(sd);
			await SendPost("startStream", JsonConvert.SerializeObject(glimmrData)).ConfigureAwait(false);
			_ep = IpUtil.Parse(_ipAddress, Port);
			Streaming = true;
			Log.Debug($"{_data.Tag}::Stream started: {_data.Id}.");
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
			Log.Debug($"{_data.Tag}::Stream stopped: {_data.Id}.");
		}


		public Task ReloadData() {
			var id = _data.Id;
			_sd = DataUtil.GetSystemData();
			var dev = DataUtil.GetDevice<GlimmrData>(id);
			if (dev == null) {
				return Task.CompletedTask;
			}

			_data = dev;
			_ipAddress = _data.IpAddress;
			Enable = _data.Enable;
			Log.Debug($"Reloaded LED Data for {id}: " + JsonConvert.SerializeObject(_data));
			return Task.CompletedTask;
		}


		public void Dispose() {
			Dispose(true).ConfigureAwait(true);
			GC.SuppressFinalize(this);
		}

		private Task SetColors(object sender, ColorSendEventArgs args) {
			return SetColor(args.LedColors, args.Force);
		}


		private async Task SetColor(Color[] leds, bool force = false) {
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

				var leds1 = new List<Color>();
				left = left.Reverse().ToArray();
				top = top.Reverse().ToArray();
				bottom = bottom.Reverse().ToArray();
				right = right.Reverse().ToArray();

				leds1.AddRange(left);
				leds1.AddRange(top);
				leds1.AddRange(right);
				leds1.AddRange(bottom);
				leds = leds1.ToArray();
			}


			var packet = new List<byte> {ByteUtils.IntByte(2), ByteUtils.IntByte(255)};
			foreach (var color in leds) {
				packet.Add(ByteUtils.IntByte(color.R));
				packet.Add(ByteUtils.IntByte(color.G));
				packet.Add(ByteUtils.IntByte(color.B));
			}

			try {
				await _udpClient.SendAsync(packet.ToArray(), packet.Count, _ep);
				ColorService?.Counter.Tick(Id);
			} catch (Exception e) {
				Log.Debug("Exception: " + e.Message);
			}
		}

		private void RefreshSystem() {
			_sd = DataUtil.GetSystemData();
		}


		public bool IsEnabled() {
			return _data.Enable;
		}


		private async Task SendPost(string target, string value) {
			Uri uri;
			if (string.IsNullOrEmpty(_ipAddress) && !string.IsNullOrEmpty(Id)) {
				_ipAddress = Id;
				_data.IpAddress = Id;
			}

			try {
				uri = new Uri("http://" + _ipAddress + "/api/DreamData/" + target);
				//Log.Debug($"Posting to {uri}: " + value);
			} catch (UriFormatException e) {
				Log.Warning("URI Format exception: " + e.Message);
				return;
			}


			var stringContent = new StringContent(value, Encoding.UTF8, "application/json");
			try {
				var _ = await _httpClient.PostAsync(uri, stringContent);
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