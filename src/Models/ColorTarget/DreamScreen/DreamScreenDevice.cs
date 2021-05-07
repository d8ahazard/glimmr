#region

using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using DreamScreenNet;
using DreamScreenNet.Devices;
using DreamScreenNet.Enum;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.DreamScreen {
	public class DreamScreenDevice : ColorTarget, IColorTarget {
		[DataMember] [JsonProperty] public bool Streaming { get; set; }
		public bool Testing { get; set; }
		[DataMember] [JsonProperty] public int Brightness { get; set; }
		[DataMember] [JsonProperty] public string Id { get; set; }
		[DataMember] [JsonProperty] public string IpAddress { get; set; }
		[DataMember] [JsonProperty] public string Tag { get; set; }
		[DataMember] [JsonProperty] public string DeviceTag { get; set; }
		[DataMember] [JsonProperty] public bool Enable { get; set; }
		public bool Online { get; set; }
		[DataMember] [JsonProperty] public DreamScreenData ScreenData { get; set; }
		
		private readonly DreamScreenClient _client;
		private List<List<Color>> _frameBuffer;
		private int _frameDelay;
		private readonly DreamDevice _dev;

		public DreamScreenDevice(DreamScreenData screenData, ColorService colorService) {
			ScreenData = screenData;
			colorService.ColorSendEvent += SetColor;
			_client = colorService.ControlService.GetAgent("DreamAgent");
			ScreenData = screenData;
			Brightness = screenData.Brightness;
			Id = screenData.Id;
			IpAddress = screenData.IpAddress;
			Tag = screenData.Tag;
			Enable = screenData.Enable;
			DeviceTag = screenData.DeviceTag;
			Online = SystemUtil.IsOnline(IpAddress);
			_frameDelay = screenData.FrameDelay;
			if (string.IsNullOrEmpty(IpAddress)) IpAddress = Id;
			var myIp = IPAddress.Parse(IpAddress);
			_dev = new DreamDevice(Tag) {IpAddress = myIp, DeviceGroup = screenData.GroupNumber};
		}

		public async Task StartStream(CancellationToken ct) {
			if (!Online) return;
			_frameBuffer = new List<List<Color>>();
			await _client.SetMode(_dev, DeviceMode.Video);
		}
		
		public async Task StopStream() {
			Log.Debug("Stopping stream.");
			await _client.SetMode(_dev, DeviceMode.Off);
		}

		public async void SetColor(List<Color> colors, List<Color> sectors, int arg3, bool force = false) {
			if (!ScreenData.Enable || Testing && !force) return;

			if (sectors.Count != 12) {
				sectors = ColorUtil.TruncateColors(sectors);
				
			}
			
			if (_frameDelay > 0) {
				_frameBuffer.Add(sectors);
				if (_frameBuffer.Count < _frameDelay) return; // Just buffer till we reach our count
				sectors = _frameBuffer[0];
				_frameBuffer.RemoveAt(0);	
			}
			
			await _client.SendColors(_dev, sectors).ConfigureAwait(false);
			ColorService.Counter.Tick(Id);
		}

		public Task FlashColor(Color color) {
			return Task.CompletedTask;
		}

		public Task ReloadData() {
			ScreenData = DataUtil.GetDevice(Id);
			Online = SystemUtil.IsOnline(IpAddress);
			_frameDelay = ScreenData.FrameDelay;
			Enable = ScreenData.Enable;
			
			return Task.CompletedTask;
		}

		public void Dispose() {
		}

		IColorTargetData IColorTarget.Data {
			get => ScreenData;
			set => ScreenData = (DreamScreenData) value;
		}

		public DreamScreenDevice(DreamScreenData screenData) {
			ScreenData = screenData;
			Brightness = screenData.Brightness;
			Id = screenData.Id;
			IpAddress = screenData.IpAddress;
			Tag = screenData.Tag;
			Enable = screenData.Enable;
			DeviceTag = screenData.DeviceTag;
		}

		public byte[] EncodeState() {
			return ScreenData.EncodeState();
		}

		
	}
}