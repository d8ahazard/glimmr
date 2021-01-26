#region

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
	public class DreamDevice : IStreamingDevice {
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

		public DreamDevice(DreamData data, DreamUtil util, ColorService colorService) {
			Data = data;
			colorService.ColorSendEvent += SetColor;
			_dreamUtil = util;
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
		}

		public async Task SetColor(object o, DynamicEventArgs dynamicEventArgs) {
			if (!Data.Enable || Testing) return;

			var sectors = dynamicEventArgs.P2;
			if (sectors.Count == 28) {
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

		StreamingData IStreamingDevice.Data {
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