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
		[DataMember] [JsonProperty] public DreamScreenData Data { get; set; }
		[DataMember] [JsonProperty] public string DeviceTag { get; set; }

		private readonly DreamScreenClient _client;
		private readonly ColorService _colorService;
		private readonly DreamDevice _dev;

		public DreamScreenDevice(DreamScreenData data, ColorService colorService) {
			Data = data;
			Id = data.Id;
			_colorService = colorService;
			colorService.ColorSendEvent += SetColor;
			_client = colorService.ControlService.GetAgent("DreamAgent");
			IpAddress = Data.IpAddress;
			DeviceTag = Data.DeviceTag;
			LoadData();
			var myIp = IPAddress.Parse(IpAddress);
			_dev = new DreamDevice(DeviceTag) {IpAddress = myIp, DeviceGroup = data.GroupNumber};
		}

		public DreamScreenDevice(DreamScreenData data) {
			Data = data;
			Id = data.Id;
			LoadData();
		}

		[DataMember] [JsonProperty] public bool Streaming { get; set; }
		public bool Testing { get; set; }
		[DataMember] [JsonProperty] public int Brightness { get; set; }
		[DataMember] [JsonProperty] public string Id { get; set; }
		[DataMember] [JsonProperty] public string IpAddress { get; set; }
		[DataMember] [JsonProperty] public string Tag { get; set; }
		[DataMember] [JsonProperty] public bool Enable { get; set; }


		public async Task StartStream(CancellationToken ct) {
			if (!Enable) {
				return;
			}

			Log.Information($"{Data.Tag}::Starting stream: {Data.Id}...");
			if (Data.DeviceTag.Contains("DreamScreen")) {
				Log.Warning("Error, you can't send colors to a dreamscreen.");
				Enable = false;
				return;
			}

			await _client.SetMode(_dev, DeviceMode.Video);
			Log.Information($"{Data.Tag}::Stream started: {Data.Id}.");
		}

		public async Task StopStream() {
			if (!Streaming) {
				return;
			}

			await _client.SetMode(_dev, DeviceMode.Off);
			Log.Information($"{Data.Tag}::Stream stopped: {Data.Id}.");
		}

		public async void SetColor(List<Color> colors, List<Color> sectors, int arg3, bool force = false) {
			if (!Data.Enable || Testing && !force) {
				return;
			}

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
			
			return Task.CompletedTask;
		}

		private void LoadData() {
			Enable = Data.Enable;
			Brightness = Data.Brightness;
			Id = Data.Id;
			IpAddress = Data.IpAddress;
			Tag = Data.Tag;
			Enable = Data.Enable;
			DeviceTag = Data.DeviceTag;
			if (DeviceTag.Contains("DreamScreen") && Enable) Enable = false;
			if (string.IsNullOrEmpty(IpAddress)) {
				IpAddress = Id;
			}
			
		}

		public void Dispose() {
		}

		IColorTargetData IColorTarget.Data {
			get => Data;
			set => Data = (DreamScreenData) value;
		}

		public byte[] EncodeState() {
			return Data.EncodeState();
		}
	}
}