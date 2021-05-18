using System;
using System.Collections.Generic;
using System.Drawing;
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
		public bool Online { get; set; }

		private YeelightData _data;

		private CaptureMode _capMode;
		private readonly ColorService _colorService;
		//private Socket _yeeSocket;

		private int _targetSector;
		private int _sectorCount;
		
		
		IColorTargetData IColorTarget.Data {
			get => _data;
			set => _data = (YeelightData) value;
		}

		private Device _yeeDevice;

		public YeelightDevice(YeelightData yd, ColorService cs) : base(cs) {
			_data = yd;
			Id = _data.Id;
			LoadData().ConfigureAwait(true);
			Log.Debug("Created new yeedevice at " + yd.IpAddress);
			cs.ColorSendEvent += SetColor;
			cs.ControlService.RefreshSystemEvent += RefreshSystem;
			_colorService = cs;
			//_yeeSocket = cs.ControlService.YeeSocket;
		}

		private void RefreshSystem() {
			var sd = DataUtil.GetSystemData();
			_capMode = (CaptureMode) sd.CaptureMode;
			_sectorCount = sd.SectorCount;
		}

		public async Task StartStream(CancellationToken ct) {
			if (!Enable) {
				Log.Warning("YEE: Not enabled!");
				return;
			}
			//Online = SystemUtil.IsOnline(IpAddress);
			//if (!Online) return;
			Streaming = await _yeeDevice.Connect();
			if (Streaming) Log.Debug("YEE: Stream started!");
		}

		public async Task StopStream() {
			if (!Streaming || !Enable) {
				return;
			}

			Log.Debug("YEE: Resetting...");
			await FlashColor(Color.FromArgb(0, 0, 0)).ConfigureAwait(false);
			_yeeDevice.Disconnect();
			Streaming = false;
		}

		public void SetColor(List<Color> colors, List<Color> sectors, int arg3, bool force=false) {
			if (!Streaming || !Enable) return;
			
			if (!force) {
				if (!Streaming || _targetSector == -1 || Testing) {
					return;
				}
			}

			var target = _targetSector * 1f;
			if (_capMode == CaptureMode.DreamScreen) {
				float tPct = target / _sectorCount;
				target =(int) (tPct * 12f);
				target = Math.Min(target, 11);
			}
		
			var col = sectors[(int)target];
			if (target >= sectors.Count) return;
			_yeeDevice.SetRGBColor(col.R, col.G, col.B).ConfigureAwait(false);
			_colorService.Counter.Tick(Id);
		}

		public async Task FlashColor(Color col) {
			await _yeeDevice.SetRGBColor(col.R, col.G, col.B).ConfigureAwait(false);
		}

		private async Task LoadData() {
			RefreshSystem();
			var prevIp = IpAddress;
			var restart = false;
			IpAddress = _data.IpAddress;
			if (!string.IsNullOrEmpty(prevIp) && prevIp != IpAddress) {
				Log.Debug("Restarting yee device...");
				if (Streaming) {
					restart = true;
					await StopStream();
					_yeeDevice?.Dispose();
				}
			}
			
			_targetSector = _data.TargetSector;
			Tag = _data.Tag;
			Id = _data.Id;
			Brightness = _data.Brightness;

			_yeeDevice ??= new Device(IpAddress);
			if (restart) await StartStream(CancellationToken.None);
		}


		public Task ReloadData() {
			_data = DataUtil.GetDevice<YeelightData>(Id);
			return Task.CompletedTask;
		}


		public void Dispose() {
			_yeeDevice.Dispose();
		}
	}
}