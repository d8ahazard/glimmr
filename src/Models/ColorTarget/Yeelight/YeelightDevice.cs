using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using YeelightAPI;

namespace Glimmr.Models.ColorTarget.Yeelight {
	public class YeelightDevice : IStreamingDevice {
		public bool Streaming { get; set; }
		public bool Testing { get; set; }
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; }
		public bool Enable { get; set; }

		private YeelightData _data;
		
		StreamingData IStreamingDevice.Data {
			get => _data;
			set => _data = (YeelightData) value;
		}

		private Device _yeeDevice;

		public YeelightDevice(YeelightData yd, ColorService cs) {
			_data = yd;
			_yeeDevice = new Device(yd.IpAddress);
			cs.ColorSendEvent += SetColor;
			 
		}
		public async Task StartStream(CancellationToken ct) {
			Streaming = await _yeeDevice.Connect();
		}

		public Task StopStream() {
			if (!Streaming) {
				return Task.CompletedTask;
			}

			_yeeDevice.Disconnect();
			Streaming = false;
			return Task.CompletedTask;
		}

		public void SetColor(List<Color> colors, List<Color> sectors, int arg3) {
			if (!Streaming || _data.TargetSector == -1 || Testing) {
				return;
			}

			var col = sectors[_data.TargetSector];
			_yeeDevice.SetRGBColor(col.R, col.G, col.B);
		}

		public async Task FlashColor(Color col) {
			await _yeeDevice.SetRGBColor(col.R, col.G, col.B);
		}

		public Task ReloadData() {
			Streaming = false;
			_yeeDevice.Dispose();
			_data = DataUtil.GetCollectionItem<YeelightData>("Dev_Yeelight", _data.Id);
			_yeeDevice = new Device(_data.IpAddress);
			return Task.CompletedTask;
		}

		public void Dispose() {
			_yeeDevice.Dispose();
		}
	}
}