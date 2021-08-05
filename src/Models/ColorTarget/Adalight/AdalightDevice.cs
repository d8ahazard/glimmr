#region

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Adalight {
	public class AdalightDevice : ColorTarget, IColorTarget {
		private AdalightData _data;
		private AdalightNet.Adalight _adalight;

		private int _baud;
		private int _ledCount;
		private int _offset;
		private int _port;
		private bool _reverseStrip;
		private int _multiplier;

		public AdalightDevice(AdalightData data, ColorService cs) {
			Id = data.Id;
			_data = data;
			IpAddress = data.IpAddress;
			LoadData();
			cs.ColorSendEvent += SetColor;
			_adalight = new AdalightNet.Adalight(_port, _ledCount, _baud);
		}

		public bool Enable { get; set; }
		public bool Streaming { get; set; }
		public bool Testing { get; set; }
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; } = "Adalight";

		IColorTargetData IColorTarget.Data {
			get => _data;
			set => _data = (AdalightData) value;
		}

		public async Task StartStream(CancellationToken ct) {
			if (Streaming || !Enable) {
				return;
			}

			Log.Information($"{_data.Tag}::Starting stream: {_data.Id}...");
			_adalight.Connect();
			_adalight.UpdateBrightness(Brightness);
			Streaming = true;
			await Task.FromResult(true);
			Log.Information($"{_data.Tag}::Stream started: {_data.Id}.");
		}

		public async Task StopStream() {
			if (!Streaming) {
				return;
			}

			Log.Information($"{_data.Tag}::Stream stopped: {_data.Id}.");
			var blacks = ColorUtil.EmptyList(_ledCount);
			_adalight.UpdateColors(blacks);
			_adalight.Disconnect();
			await Task.FromResult(true);
			Streaming = false;
			Log.Information($"{_data.Tag}::Stream stopped: {_data.Id}.");
		}

		public void SetColor(List<Color> colors, List<Color> sectors, int fadeTime, bool force = false) {
			if (!Enable || !Streaming || Testing && !force) {
				return;
			}

			var toSend = ColorUtil.TruncateColors(colors, _offset, _ledCount).ToList();
			if (_reverseStrip) {
				toSend.Reverse();
			}

			_adalight.UpdateColors(toSend);
		}

		public async Task FlashColor(Color color) {
			var toSend = ColorUtil.FillArray(color, _ledCount);
			_adalight.UpdateColors(toSend.ToList());
			await Task.FromResult(true);
		}

		public Task ReloadData() {
			var oldBaud = _baud;
			var oldPort = _port;
			var oldCount = _ledCount;
			var dev = DataUtil.GetDevice<AdalightData>(Id);
			
			if (dev == null) {
				return Task.CompletedTask;
			}

			_data = dev;
			LoadData();
			if (oldBaud != _baud || oldPort != _port || oldCount != _ledCount) {
				Log.Debug("Reloading connection to adalight!");
				var wasStreaming = Streaming;
				if (Streaming) {
					_adalight.Disconnect();
				}

				Streaming = false;
				_adalight = new AdalightNet.Adalight(_port, _ledCount, _baud);
				if (wasStreaming) {
					Streaming = _adalight.Connect();
				}
			}

			if (_adalight != null && _adalight.Connected) {
				_adalight.UpdateBrightness(Brightness);
			}

			return Task.CompletedTask;
		}

		public void Dispose() {
			_adalight.Dispose();
		}

		private void LoadData() {
			_offset = _data.Offset;
			_ledCount = _data.LedCount;
			_reverseStrip = _data.ReverseStrip;
			_baud = _data.Speed;
			_port = _data.Port;
			Enable = _data.Enable;
			if (_data.Brightness == 0) {
				Brightness = 0;
			} else {
				Brightness = (int) (_data.Brightness / 100f * 255);
			}
		}
	}
}