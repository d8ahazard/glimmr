﻿#region

using System.Drawing;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DreamScreenNet;
using DreamScreenNet.Devices;
using DreamScreenNet.Enum;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.DreamScreen {
	public class DreamScreenDevice : ColorTarget, IColorTarget {
		private readonly DreamScreenClient? _client;
		private readonly DreamDevice _dev;
		private DreamScreenData _data;
		private string _deviceTag;
		private string _ipAddress;

		public DreamScreenDevice(DreamScreenData data, ColorService colorService) {
			_data = data;
			Id = data.Id;
			colorService.ColorSendEventAsync += SetColors;
			var client = colorService.ControlService.GetAgent("DreamAgent");
			if (client != null) {
				_client = client;
			}

			_ipAddress = _data.IpAddress;
			_deviceTag = _data.DeviceTag;
			LoadData();
			var myIp = IPAddress.Parse(_ipAddress);
			_dev = new DreamDevice(_deviceTag) {IpAddress = myIp, DeviceGroup = data.GroupNumber};
		}
		
		private Task SetColors(object sender, ColorSendEventArgs args) {
			SetColor(args.LedColors, args.SectorColors, args.FadeTime, args.Force);
			return Task.CompletedTask;
		}

		public bool Streaming { get; set; }
		public bool Testing { get; set; }
		public string Id { get; private set; }
		public bool Enable { get; set; }

		public async Task StartStream(CancellationToken ct) {
			if (!Enable) {
				return;
			}

			if (_client == null) {
				return;
			}

			Log.Information($"{_data.Tag}::Starting stream: {_data.Id}...");
			if (_data.DeviceTag.Contains("DreamScreen")) {
				Log.Warning("Error, you can't send colors to a dreamscreen.");
				Enable = false;
				return;
			}

			await _client.SetMode(_dev, DeviceMode.Video);
			Log.Information($"{_data.Tag}::Stream started: {_data.Id}.");
		}

		public async Task StopStream() {
			if (!Streaming) {
				return;
			}

			if (_client == null) {
				return;
			}

			await _client.SetMode(_dev, DeviceMode.Off);
			Log.Information($"{_data.Tag}::Stream stopped: {_data.Id}.");
		}

		
		
			public async void SetColor(Color[] colors, Color[] sectors, int arg3, bool force = false) {
			if (!_data.Enable || Testing && !force) {
				return;
			}

			if (_client == null) {
				return;
			}

			if (sectors.Length != 12) {
				sectors = ColorUtil.TruncateColors(sectors);
			}

			await _client.SendColors(_dev, sectors).ConfigureAwait(false);
		}

		public Task FlashColor(Color color) {
			return Task.CompletedTask;
		}

		public Task ReloadData() {
			var dev = DataUtil.GetDevice(Id);
			if (dev != null) {
				_data = dev;
			}

			return Task.CompletedTask;
		}

		public void Dispose() {
		}

		IColorTargetData IColorTarget.Data {
			get => _data;
			set => _data = (DreamScreenData) value;
		}

		private void LoadData() {
			Enable = _data.Enable;
			Id = _data.Id;
			_ipAddress = _data.IpAddress;
			Enable = _data.Enable;
			_deviceTag = _data.DeviceTag;
			if (_deviceTag.Contains("DreamScreen") && Enable) {
				Enable = false;
			}

			if (string.IsNullOrEmpty(_ipAddress)) {
				_ipAddress = Id;
			}
		}
	}
}