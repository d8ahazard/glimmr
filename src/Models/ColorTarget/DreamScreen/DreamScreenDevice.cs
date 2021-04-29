#region

using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using DreamScreenNet;
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
		[DataMember] [JsonProperty] public DreamScreenData ScreenData { get; set; }

		private readonly DreamScreenClient _client;
		private readonly IPAddress _myIp;
		private List<List<Color>> _frameBuffer;
		private int _frameDelay;
		

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
			if (string.IsNullOrEmpty(IpAddress)) IpAddress = Id;
			_myIp = IPAddress.Parse(IpAddress);
		}

		public async Task StartStream(CancellationToken ct) {
			_frameBuffer = new List<List<Color>>();
			await _client.SetMode(DeviceMode.Video, _myIp, ScreenData.GroupNumber);
		}
		
		public async Task StopStream() {
			Log.Debug("Stopping stream.");
			await _client.SetMode(DeviceMode.Off, _myIp, ScreenData.GroupNumber);
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
			
			await _client.SendColors(_myIp, ScreenData.GroupNumber, sectors).ConfigureAwait(false);
			ColorService.Counter.Tick(Id);
		}

		public Task FlashColor(Color color) {
			return Task.CompletedTask;
		}

		public Task ReloadData() {
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