#region

using System;
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
		private byte[] _gammaTable;
		private int _ledCount;
		private float _multiplier;
		private int _offset;
		private string _port;
		private bool _reverseStrip;

		public AdalightDevice(AdalightData data, ColorService cs) : base(cs) {
			Id = data.Id;
			_data = data;
			_port = _data.Port;
			_multiplier = _data.LedMultiplier;
			_gammaTable = ColorUtil.GammaTable(1);
			LoadData();
			cs.ColorSendEventAsync += SetColors;
			_adalight = new AdalightNet.Adalight(_port, _ledCount, _baud);
		}

		public bool Enable { get; set; }
		public bool Streaming { get; set; }
		public bool Testing { get; set; }
		public string Id { get; }

		IColorTargetData IColorTarget.Data {
			get => _data;
			set => _data = (AdalightData)value;
		}

		public async Task StartStream(CancellationToken ct) {
			if (Streaming || !Enable) {
				return;
			}

			Log.Debug($"{_data.Tag}::Starting stream: {_data.Id}...");
			ColorService.StartCounter++;
			var conn = _adalight.Connect();
			if (!conn) {
				Log.Warning("Unable to connect!");
				return;
			}

			Streaming = true;
			await Task.FromResult(true);
			Log.Debug($"{_data.Tag}::Stream started: {_data.Id}.");
			ColorService.StartCounter--;
		}

		public async Task StopStream() {
			if (!Streaming) {
				return;
			}

			Log.Debug($"{_data.Tag}::Stopping stream: {_data.Id}.");
			ColorService.StopCounter++;
			var blacks = ColorUtil.EmptyColors(_ledCount);
			await _adalight.UpdateColorsAsync(blacks.ToList());
			_adalight.Disconnect();
			Streaming = false;
			Log.Debug($"{_data.Tag}::Stream stopped: {_data.Id}.");
			ColorService.StopCounter--;
		}

		public async Task FlashColor(Color color) {
			var toSend = ColorUtil.FillArray(color, _ledCount);
			await _adalight.UpdateColorsAsync(toSend.ToList());
		}

		public Task ReloadData() {
			var oldBaud = _baud;
			var oldPort = _port;
			var oldCount = _ledCount;
			var dev = DataUtil.GetDevice<AdalightData>(Id);

			if (dev == null) {
				Log.Warning("Unable to retrieve ada data.");
				return Task.CompletedTask;
			}

			_data = dev;
			_brightness = _data.Brightness;
			LoadData();
			if (oldBaud == _baud && oldPort == _port && oldCount == _ledCount) {
				return Task.CompletedTask;
			}

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

			return Task.CompletedTask;
		}

		public void Dispose() {
			_adalight.Dispose();
		}

		private Task SetColors(object sender, ColorSendEventArgs args) {
			SetColor(args.LedColors, args.Force);
			return Task.CompletedTask;
		}


		private void SetColor(Color[] colors, bool force = false) {
			if (!Enable || !Streaming || Testing && !force) {
				return;
			}

			var toSend = ColorUtil.TruncateColors(colors, _offset, _ledCount, _multiplier);
			if (_reverseStrip) {
				toSend = toSend.Reverse().ToArray();
			}

			if (_brightness < 100) {
				toSend = ColorUtil.AdjustBrightness(toSend, _brightness / 100f);
			}

			toSend = FixGamma(toSend);

			_adalight.UpdateColorsAsync(toSend.ToList());
		}

		private Color[] FixGamma(Color[] input) {
			if (Math.Abs(_data.GammaFactor - 1.0f) < float.MinValue) {
				return input;
			}

			var output = new Color[input.Length];
			for (var i = 0; i < input.Length; i++) {
				var col = input[i];
				output[i] = Color.FromArgb(0, _gammaTable[col.R], _gammaTable[col.G], _gammaTable[col.B]);
			}

			return output;
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

			_gammaTable = ColorUtil.GammaTable(_data.GammaFactor);
			_brightness = _data.Brightness;
		}
	}
}