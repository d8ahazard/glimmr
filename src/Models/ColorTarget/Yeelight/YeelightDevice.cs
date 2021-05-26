using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;
using YeelightAPI;

namespace Glimmr.Models.ColorTarget.Yeelight {
	public class YeelightDevice : ColorTarget, IColorTarget {
		private readonly ColorService _colorService;

		public YeelightData Data;

		private CaptureMode _capMode;
		private int _sectorCount;
		private Task _streamTask;

		private int _targetSector;

		private Device _yeeDevice;

		public YeelightDevice(YeelightData yd, ColorService cs) : base(cs) {
			Data = yd;
			Id = Data.Id;
			LoadData();
			Log.Debug("Created new yeedevice at " + yd.IpAddress);
			cs.ColorSendEvent += SetColor;
			_colorService = cs;

			Data.LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
			DataUtil.AddDeviceAsync(Data, false).ConfigureAwait(false);
		}

		public bool Streaming { get; set; }
		public bool Testing { get; set; }
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; }
		public bool Enable { get; set; }

		private bool _isOn;


		IColorTargetData IColorTarget.Data {
			get => Data;
			set => Data = (YeelightData) value;
		}


		public async Task StartStream(CancellationToken ct) {
			if (!Enable) {
				Log.Warning("YEE: Not enabled!");
				return;
			}

			Log.Information($"{Data.Tag}::Starting stream: {Data.Id}...");
			_targetSector = ColorUtil.CheckDsSectors(Data.TargetSector);

			await _yeeDevice.Connect();
			var ip = IpUtil.GetLocalIpAddress();
			if (!string.IsNullOrEmpty(ip)) {
				_streamTask = _yeeDevice.StartMusicMode(ip);
				Streaming = true;
			}

			if (Streaming) {
				Log.Information($"{Data.Tag}::Stream started: {Data.Id}.");
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
			Log.Information($"{Data.Tag}::Stream stopped: {Data.Id}.");
		}

		public void SetColor(List<Color> colors, List<Color> sectors, int arg3, bool force = false) {
			if (!Streaming || !Enable) {
				return;
			}

			if (!force) {
				if (!Streaming || _targetSector == -1 || Testing) {
					return;
				}
			}

			var col = sectors[_targetSector];
			if (_targetSector >= sectors.Count) {
				return;
			}

			_yeeDevice.SetRGBColor(col.R, col.G, col.B).ConfigureAwait(false);
			var bri = col.GetBrightness() * 100;
			if (bri <= 10f) {
				if (_isOn) {
					_isOn = false;
					_yeeDevice.SetPower(false);
				}
			} else {
				if (!_isOn) {
					_isOn = true;
					_yeeDevice.SetPower();
				}
				_yeeDevice.SetRGBColor(col.R, col.G, col.B).ConfigureAwait(false);
				_yeeDevice.BackgroundSetBrightness((int)bri);
			}
			
			_colorService.Counter.Tick(Id);
		}

		public async Task FlashColor(Color col) {
			await _yeeDevice.SetRGBColor(col.R, col.G, col.B).ConfigureAwait(false);
		}


		public Task ReloadData() {
			Data = DataUtil.GetDevice<YeelightData>(Id);
			return Task.CompletedTask;
		}


		public void Dispose() {
			_yeeDevice.Dispose();
		}

		private void LoadData() {
			var prevIp = IpAddress;
			var restart = false;
			IpAddress = Data.IpAddress;
			if (!string.IsNullOrEmpty(prevIp) && prevIp != IpAddress) {
				Log.Debug("Restarting yee device...");
				if (Streaming) {
					restart = true;
					StopStream().ConfigureAwait(true);
					_yeeDevice?.Dispose();
				}
			}

			_targetSector = ColorUtil.CheckDsSectors(Data.TargetSector);
			Tag = Data.Tag;
			Id = Data.Id;
			Brightness = Data.Brightness;
			Enable = Data.Enable;

			_yeeDevice ??= new Device(IpAddress);
			if (restart) {
				StartStream(CancellationToken.None).ConfigureAwait(true);
			}

			Log.Debug("YEE: Data reloaded: " + Enable);
		}
	}
}