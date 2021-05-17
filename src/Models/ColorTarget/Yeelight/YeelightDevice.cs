using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
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

		private int _sectorCount;
		
		
		IColorTargetData IColorTarget.Data {
			get => _data;
			set => _data = (YeelightData) value;
		}

		private Device _yeeDevice;

		public YeelightDevice(YeelightData yd, ColorService cs) : base(cs) {
			_data = yd;
			Tag = _data.Tag;
			Enable = _data.Enable;
			_yeeDevice = new Device(yd.IpAddress);
			Log.Debug("Created new yeedevice at " + yd.IpAddress);
			cs.ColorSendEvent += SetColor;
			cs.ControlService.RefreshSystemEvent += RefreshSystem;
			RefreshSystem();
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
				if (!Streaming || _data.TargetSector == -1 || Testing) {
					return;
				}
			}

			var target = _data.TargetSector;
			if (_capMode == CaptureMode.DreamScreen) {
				var tPct = target / _sectorCount;
				target = tPct * 12;
				target = Math.Min(target, 11);
			}
			Log.Debug($"YEE: setting color for {target}");

			var col = sectors[target];
			if (target >= sectors.Count) return;
			_yeeDevice.SetRGBColor(col.R, col.G, col.B).ConfigureAwait(false);
			ColorService.Counter.Tick(Id);
		}

		public async Task FlashColor(Color col) {
			await _yeeDevice.SetRGBColor(col.R, col.G, col.B).ConfigureAwait(false);
		}


		public Task ReloadData() {
			_yeeDevice?.Dispose();
			_data = DataUtil.GetDevice<YeelightData>(_data.Id);
			_yeeDevice = new Device(_data.IpAddress);
			Brightness = _data.Brightness;
			Enable = _data.Enable;
			RefreshSystem();
			return Task.CompletedTask;
		}


		public void Dispose() {
			_yeeDevice.Dispose();
		}
	}
}