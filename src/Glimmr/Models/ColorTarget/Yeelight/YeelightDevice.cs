#region

using System;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;
using YeelightAPI;

#endregion

namespace Glimmr.Models.ColorTarget.Yeelight {
	public class YeelightDevice : ColorTarget, IColorTarget {
		private string IpAddress { get; set; }
		private readonly ColorService _colorService;

		private readonly Device _yeeDevice;

		private YeelightData _data;

		private Task? _streamTask;

		private int _targetSector;


		public YeelightDevice(YeelightData yd, ColorService cs) : base(cs) {
			_data = yd;
			Id = _data.Id;
			IpAddress = _data.IpAddress;
			Enable = _data.Enable;
			LoadData();
			Log.Debug("Created new yee device at " + yd.IpAddress);
			cs.ColorSendEventAsync += SetColors;
			_colorService = cs;

			_data.LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
			DataUtil.AddDeviceAsync(_data, false).ConfigureAwait(false);
			_yeeDevice = new Device(IpAddress);
		}

		private Task SetColors(object sender, ColorSendEventArgs args) {
			return SetColor(args.SectorColors, args.Force);
		}

		public bool Streaming { get; set; }
		public bool Testing { get; set; }
		public string Id { get; private set; }
		public bool Enable { get; set; }


		IColorTargetData IColorTarget.Data {
			get => _data;
			set => _data = (YeelightData) value;
		}


		public async Task StartStream(CancellationToken ct) {
			if (!Enable) {
				Log.Warning("YEE: Not enabled!");
				return;
			}

			Log.Information($"{_data.Tag}::Starting stream: {_data.Id}...");
			_targetSector = _data.TargetSector;

			await _yeeDevice.Connect();
			var ip = IpUtil.GetLocalIpAddress();
			if (!string.IsNullOrEmpty(ip)) {
				_streamTask = _yeeDevice.StartMusicMode(ip);
				Streaming = true;
			}

			if (Streaming) {
				Log.Information($"{_data.Tag}::Stream started: {_data.Id}.");
			}
		}

		public async Task StopStream() {
			if (!Streaming) {
				return;
			}

			await FlashColor(Color.FromArgb(0, 0, 0)).ConfigureAwait(false);
			await _yeeDevice.StopMusicMode();
			_yeeDevice.Disconnect();
			Streaming = false;
			if (_streamTask != null) {
				if (!_streamTask.IsCompleted) {
					_streamTask.Dispose();
				}
			}

			Log.Information($"{_data.Tag}::Stream stopped: {_data.Id}.");
		}

		private async Task SetColor(Color[] sectors, bool force = false) {
			if (!Streaming || !Enable) {
				return;
			}

			if (!force) {
				if (!Streaming || _targetSector == -1 || Testing) {
					return;
				}
			}
			
			// TODO: Clamp brightness here.
			var col = sectors[_targetSector];
			if (_targetSector >= sectors.Length) {
				return;
			}
			
			await _yeeDevice.SetRGBColor(col.R, col.G, col.B);
			_colorService.Counter.Tick(Id);
		}


		public async Task FlashColor(Color col) {
			await _yeeDevice.SetRGBColor(col.R, col.G, col.B).ConfigureAwait(false);
		}


		public Task ReloadData() {
			var dd = DataUtil.GetDevice<YeelightData>(Id);
			_data = dd ?? _data;
			return Task.CompletedTask;
		}


		public void Dispose() {
			_yeeDevice.Dispose();
		}

		private void LoadData() {
			var sd = DataUtil.GetSystemData();
			var prevIp = IpAddress;
			var restart = false;
			IpAddress = _data.IpAddress;
			if (!string.IsNullOrEmpty(prevIp) && prevIp != IpAddress) {
				Log.Debug("Restarting yee device...");
				if (Streaming) {
					restart = true;
					StopStream().ConfigureAwait(true);
					_yeeDevice.Dispose();
				}
			}

			var target = _data.TargetSector;

			if (sd.UseCenter) {
				target = ColorUtil.FindEdge(target + 1);
			}

			_targetSector = target;
			Id = _data.Id;
			Enable = _data.Enable;

			if (restart) {
				StartStream(CancellationToken.None).ConfigureAwait(true);
			}

			Log.Debug("YEE: Data reloaded: " + Enable);
		}
	}
}