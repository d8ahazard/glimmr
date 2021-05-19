#region
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using DreamScreenNet;
using DreamScreenNet.Devices;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Serilog;
using DeviceMode = DreamScreenNet.Enum.DeviceMode;

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
		[DataMember] [JsonProperty] public DreamScreenData Data { get; set; }
		
		private readonly DreamScreenClient _client;
		private readonly DreamDevice _dev;
		private readonly ColorService _colorService;

		public DreamScreenDevice(DreamScreenData data, ColorService colorService) {
			Data = data;
			_colorService = colorService;
			colorService.ColorSendEvent += SetColor;
			_client = colorService.ControlService.GetAgent("DreamAgent");
			Data = data;
			Brightness = data.Brightness;
			Id = data.Id;
			IpAddress = data.IpAddress;
			Tag = data.Tag;
			Enable = data.Enable;
			DeviceTag = data.DeviceTag;
			if (string.IsNullOrEmpty(IpAddress)) IpAddress = Id;
			var myIp = IPAddress.Parse(IpAddress);
			_dev = new DreamDevice(Tag) {IpAddress = myIp, DeviceGroup = data.GroupNumber};
		
		}
		
	
		public async Task StartStream(CancellationToken ct) {
			if (Data.DeviceTag.Contains("DreamScreen")) {
				Log.Warning("Error, you can't send colors to a dreamscreen.");
				Enable = false;
				return;
			}
			if (!Enable) return;
			await _client.SetMode(_dev, DeviceMode.Video);
		}
		
		public async Task StopStream() {
			if (!Enable) return;
			Log.Debug("Stopping stream.");
			await _client.SetMode(_dev, DeviceMode.Off);
		}

		public async void SetColor(List<Color> colors, List<Color> sectors, int arg3, bool force = false) {
			if (!Data.Enable || Testing && !force) return;

			if (sectors.Count != 12) {
				sectors = ColorUtil.TruncateColors(sectors);
			}
			
			await _client.SendColors(_dev, sectors).ConfigureAwait(false);
			_colorService.Counter.Tick(Id);
		}

		public Task FlashColor(Color color) {
			return Task.CompletedTask;
		}

		public Task ReloadData() {
			Data = DataUtil.GetDevice(Id);
			Enable = Data.Enable;
			return Task.CompletedTask;
		}

		public void Dispose() {
		}

		IColorTargetData IColorTarget.Data {
			get => Data;
			set => Data = (DreamScreenData) value;
		}

		public DreamScreenDevice(DreamScreenData data) {
			Data = data;
			Brightness = data.Brightness;
			Id = data.Id;
			IpAddress = data.IpAddress;
			Tag = data.Tag;
			Enable = data.Enable;
			DeviceTag = data.DeviceTag;
		}

		public byte[] EncodeState() {
			return Data.EncodeState();
		}

		
	}
}