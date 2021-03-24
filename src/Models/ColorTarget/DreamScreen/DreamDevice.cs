#region

using System.Collections.Generic;
using System.Drawing;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.DreamScreen {
	public class DreamDevice : ColorTarget, IColorTarget {
		[DataMember] [JsonProperty] public bool Streaming { get; set; }
		public bool Testing { get; set; }
		[DataMember] [JsonProperty] public int Brightness { get; set; }
		[DataMember] [JsonProperty] public string Id { get; set; }
		[DataMember] [JsonProperty] public string IpAddress { get; set; }
		[DataMember] [JsonProperty] public string Tag { get; set; }
		[DataMember] [JsonProperty] public string DeviceTag { get; set; }
		[DataMember] [JsonProperty] public bool Enable { get; set; }
		[DataMember] [JsonProperty] public DreamData Data { get; set; }
		
		
		
		private readonly DreamUtil _dreamUtil;

		private Task _subTask;

		public DreamDevice(DreamData data, ColorService colorService) {
			Data = data;
			colorService.ColorSendEvent += SetColor;
			_dreamUtil = colorService.ControlService.GetAgent("DreamAgent");
			Data = data;
			Brightness = data.Brightness;
			Id = data.Id;
			IpAddress = data.IpAddress;
			Tag = data.Tag;
			Enable = data.Enable;
			DeviceTag = data.DeviceTag;
			if (string.IsNullOrEmpty(IpAddress)) IpAddress = Id;
		}

		public async Task StartStream(CancellationToken ct) {
			await _dreamUtil.SendMessage("mode", 1, Id);
		}
		
		

		public async Task StopStream() {
			Log.Debug("Stopping stream.");
			await _dreamUtil.SendMessage("mode", 0, Id);
			_subTask?.Dispose();
		}

		public async void SetColor(List<Color> colors, List<Color> sectors, int arg3, bool force = false) {
			if (!Data.Enable || Testing && !force) return;

			if (sectors.Count != 12) {
				sectors = ColorUtil.TruncateColors(sectors);
				
			}
			await _dreamUtil.SendSectors(sectors, Id, Data.DeviceGroup);
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
			get => Data;
			set => Data = (DreamData) value;
		}

		public DreamDevice(DreamData data) {
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