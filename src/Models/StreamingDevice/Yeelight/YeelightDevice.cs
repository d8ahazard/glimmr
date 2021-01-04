using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using Glimmr.Models.Util;
using Microsoft.AspNetCore.Builder;
using YeelightAPI;

namespace Glimmr.Models.StreamingDevice.Yeelight {
	public class YeelightDevice : IStreamingDevice {
		public bool Streaming { get; set; }
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

		public YeelightDevice(YeelightData yd) {
			_data = yd;
			_yeeDevice = new Device(yd.IpAddress);
		}
		public void StartStream(CancellationToken ct) {
			Streaming = _yeeDevice.Connect().Result;
		}

		public void StopStream() {
			if (!Streaming) {
				return;
			}

			_yeeDevice.Disconnect();
			Streaming = false;
		}

		public void SetColor(List<Color> _, List<Color> sectors, double fadeTime) {
			if (!Streaming || _data.TargetSector == -1) {
				return;
			}

			var col = sectors[_data.TargetSector];
			_yeeDevice.SetRGBColor(col.R, col.G, col.B);
		}

		public void ReloadData() {
			Streaming = false;
			_yeeDevice.Dispose();
			_data = DataUtil.GetCollectionItem<YeelightData>("Dev_Yeelight", _data.Id);
			_yeeDevice = new Device(_data.IpAddress);
		}

		public void Dispose() {
			_yeeDevice.Dispose();
		}
	}
}