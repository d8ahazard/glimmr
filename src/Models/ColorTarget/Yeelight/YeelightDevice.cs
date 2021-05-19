using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;
using YeelightAPI;

namespace Glimmr.Models.ColorTarget.Yeelight {
	public class YeelightDevice : ColorTarget, IColorTarget {
		public bool Streaming { get; set; }
		public bool Testing { get; set; }
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; }
		public bool Enable { get; set; }
		
		public YeelightData Data;

		private CaptureMode _capMode;
		private readonly ColorService _colorService;
		private Task _streamTask;

		private int _targetSector;
		private int _sectorCount;
		
		
		IColorTargetData IColorTarget.Data {
			get => Data;
			set => Data = (YeelightData) value;
		}

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

		

		
		public async Task StartStream(CancellationToken ct) {
			Log.Debug("Startstream called!");
			if (!Enable) {
				Log.Warning("YEE: Not enabled!");
				return;
			}
			_targetSector = ColorUtil.CheckDsSectors(Data.TargetSector);

			Log.Debug("Connecting.");
			await _yeeDevice.Connect();
			Log.Debug("Connected.");
			var ip = IpUtil.GetLocalIpAddress();
			Log.Debug("Trying to start music: " + ip);
			if (!string.IsNullOrEmpty(ip)) {
				Log.Debug("Creating stream task?");
				_streamTask = _yeeDevice.StartMusicMode(ip);
				Log.Debug("Task created!");
				Streaming = true;
			} else {
				Log.Debug("Can't get local IP: " + ip);
			}

			if (Streaming) {
				Log.Debug("YEE: Stream started!");
			} else {
				Log.Debug("Streaming was not started..." + Enable);
			}
		}

		public async Task StopStream() {
			if (!Streaming || !Enable) {
				return;
			}

			Log.Debug("YEE: Resetting...");
			await FlashColor(Color.FromArgb(0, 0, 0)).ConfigureAwait(false);
			Log.Debug("YEE: Stopping music mode...");
			await _yeeDevice.StopMusicMode();
			Log.Debug("Yee: Stopped!");
			if (_streamTask.IsCompleted) Log.Debug("Stream task is completed.");
			_yeeDevice.Disconnect();
			Log.Debug("Disconnected!");
			Streaming = false;
			Log.Debug("YEE: Streaming stopped.");
		}

		public void SetColor(List<Color> colors, List<Color> sectors, int arg3, bool force=false) {
			if (!Streaming || !Enable) return;
			
			if (!force) {
				if (!Streaming || _targetSector == -1 || Testing) {
					return;
				}
			}

			var col = sectors[_targetSector];
			if (_targetSector >= sectors.Count) return;
			int min = Math.Max(col.R, Math.Max(col.G, col.B)) / 255 * 100;
			_yeeDevice.SetRGBColor(col.R, col.G, col.B).ConfigureAwait(false);
			_yeeDevice.SetBrightness(min);
			_colorService.Counter.Tick(Id);
		}

		public async Task FlashColor(Color col) {
			await _yeeDevice.SetRGBColor(col.R, col.G, col.B).ConfigureAwait(false);
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
			if (restart) StartStream(CancellationToken.None).ConfigureAwait(true);
			Log.Debug("YEE: Data reloaded: " + Enable);
		}


		public Task ReloadData() {
			Data = DataUtil.GetDevice<YeelightData>(Id);
			return Task.CompletedTask;
		}


		public void Dispose() {
			_yeeDevice.Dispose();
		}
	}
}