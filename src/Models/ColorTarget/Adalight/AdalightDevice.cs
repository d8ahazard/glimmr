using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;

namespace Glimmr.Models.ColorTarget.Adalight {
	public class AdalightDevice : ColorTarget, IColorTarget {
		public bool Enable { get; set; }
		public bool Streaming { get; set; }
		public bool Testing { get; set; }
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; }
		
		private int _baud;
		private int _ledCount;
		private bool _reverseStrip;
		private int _offset;
		private int _port;
		public AdalightData Data { get; set; }
		IColorTargetData IColorTarget.Data {
			get => Data;
			set => Data = (AdalightData) value;
		}
		private AdalightNet.Adalight _adalight;

		public AdalightDevice(AdalightData data, ColorService cs) {
			Id = data.Id;
			Data = data;
			LoadData();
			cs.ColorSendEvent += SetColor;
			_adalight = new AdalightNet.Adalight(_port, _ledCount, _baud);
		}
		
		public async Task StartStream(CancellationToken ct) {
			if (Streaming || !Enable) {
				return;
			}

			Log.Information($"{Data.Tag}::Starting stream: {Data.Id}...");
			Streaming = _adalight.Connect();
			await Task.FromResult(true);
			Log.Information($"{Data.Tag}::Stream started: {Data.Id}.");
		}

		public async Task StopStream() {
			if (!Streaming) return;
			var blacks = ColorUtil.EmptyList(_ledCount);
			_adalight.UpdateColors(blacks);
			await Task.FromResult(_adalight.Disconnect());
			Streaming = false;
			Log.Information($"{Data.Tag}::Stream stopped: {Data.Id}.");
		}

		public void SetColor(List<Color> colors, List<Color> sectors, int fadeTime, bool force = false) {
			if (!Enable || !Streaming || !force) return;
			var toSend = ColorUtil.TruncateColors(colors, _offset, _ledCount).ToList();
			if (_reverseStrip) toSend.Reverse();
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
			Data = DataUtil.GetDevice<AdalightData>(Id);
			LoadData();
			if (oldBaud != _baud || oldPort != _port || oldCount != _ledCount) {
				Log.Debug("Reloading connection to adalight!");
				var wasStreaming = Streaming;
				if (Streaming) _adalight.Disconnect();
				Streaming = false;
				_adalight = new AdalightNet.Adalight(_port, _ledCount, _baud);
				if (wasStreaming) Streaming = _adalight.Connect();

			}
			return Task.CompletedTask;
		}

		private void LoadData() {
			_offset = Data.Offset;
			_ledCount = Data.LedCount;
			_reverseStrip = Data.ReverseStrip;
			_baud = Data.Speed;
			_port = Data.Port;
			Enable = Data.Enable;
			Brightness = Data.Brightness;
		}

		public void Dispose() {
			_adalight.Dispose();
		}
	}
}