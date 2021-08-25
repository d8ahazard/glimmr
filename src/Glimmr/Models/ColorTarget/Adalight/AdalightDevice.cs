#region

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
		private AdalightNet.Adalight _adalight;

		private int _baud;
		private int _brightness;
		private AdalightData _data;
		private int _ledCount;
		private int _multiplier;
		private int _offset;
		private string _port;
		private bool _reverseStrip;

		public AdalightDevice(AdalightData data, ColorService cs) {
			Id = data.Id;
			_data = data;
			_multiplier = _data.LedMultiplier;
			LoadData();
			cs.ColorSendEventAsync += SetColors;
			_adalight = new AdalightNet.Adalight(_port, _ledCount, _baud);
		}
		
		private Task SetColors(object sender, ColorSendEventArgs args) {
			SetColor(args.LedColors, args.Force);
			return Task.CompletedTask;
		}

		public bool Enable { get; set; }
		public bool Streaming { get; set; }
		public bool Testing { get; set; }
		public string Id { get; }

		IColorTargetData IColorTarget.Data {
			get => _data;
			set => _data = (AdalightData) value;
		}

		public async Task StartStream(CancellationToken ct) {
			if (Streaming || !Enable) {
				return;
			}

			Log.Debug($"{_data.Tag}::Starting stream: {_data.Id}...");
			_adalight.Connect();
			_adalight.UpdateBrightness(_brightness);
			Streaming = true;
			await Task.FromResult(true);
			Log.Debug($"{_data.Tag}::Stream started: {_data.Id}.");
		}

		public async Task StopStream() {
			if (!Streaming) {
				return;
			}

			Log.Debug($"{_data.Tag}::Stream stopped: {_data.Id}.");
			var blacks = ColorUtil.EmptyList(_ledCount);
			_adalight.UpdateColors(blacks);
			_adalight.Disconnect();
			await Task.FromResult(true);
			Streaming = false;
			Log.Debug($"{_data.Tag}::Stream stopped: {_data.Id}.");
		}


		private void SetColor(Color[] colors, bool force = false) {
			if (!Enable || !Streaming || Testing && !force) {
				return;
			}

			var toSend = ColorUtil.TruncateColors(colors, _offset, _ledCount, _multiplier).ToList();
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
				_adalight.UpdateBrightness(_brightness);
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
			_multiplier = _data.LedMultiplier;
			if (_multiplier == 0) {
				_multiplier = 1;
			}

			if (_data.Brightness == 0) {
				_brightness = 0;
			} else {
				_brightness = (int) (_data.Brightness / 100f * 255);
			}
		}
	}
}