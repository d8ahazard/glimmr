#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.Serialization;
using System.Threading;
using Glimmr.Models.Util;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.StreamingDevice.Dreamscreen {
	public class DreamDevice : IStreamingDevice {
		[DataMember] [JsonProperty] public bool Streaming { get; set; }
		[DataMember] [JsonProperty] public int Brightness { get; set; }
		[DataMember] [JsonProperty] public string Id { get; set; }
		[DataMember] [JsonProperty] public string IpAddress { get; set; }
		[DataMember] [JsonProperty] public string Tag { get; set; }
		[DataMember] [JsonProperty] public string DeviceTag { get; set; }
		[DataMember] [JsonProperty] public bool Enable { get; set; }
		[DataMember] [JsonProperty] public DreamData Data { get; set; }

		private DreamUtil _dreamUtil;

		public DreamDevice(DreamData data, DreamUtil util) {
			Data = data;
			_dreamUtil = util;
			Data = data;
			Brightness = data.Brightness;
			Id = data.Id;
			IpAddress = data.IpAddress;
			Tag = data.Tag;
			Enable = data.Enable;
			DeviceTag = data.DeviceTag;
		}

		public void StartStream(CancellationToken ct) {
		}

		public void StopStream() {
		}

		public void SetColor(List<Color> _, List<Color> sectors, double fadeTime) {
			if (sectors.Count == 28) {
				sectors = ColorUtil.TruncateColors(sectors);
				
			}
			_dreamUtil.SendSectors(sectors, Id, Data.GroupNumber);
		}

		public void ReloadData() {
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