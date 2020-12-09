using System;
using System.ComponentModel;
using Glimmr.Models.Util;
using LifxNet;
using LiteDB;
using Newtonsoft.Json;

namespace Glimmr.Models.StreamingDevice.LIFX {
	public class LifxData : StreamingData { 
		[BsonCtor]
		public LifxData() {
			Tag = "Lifx";
			if (Id == null && MacAddressString != null) {
				Id = MacAddressString;
			}
			Name ??= Tag;
			if (Id != null) Name = "Lifx - " + Id.Substring(0, 4);
		}

		public LifxData(LightBulb b) {
			if (b == null) throw new ArgumentException("Invalid bulb data.");
			Tag = "Lifx";
			Name ??= Tag;
			HostName = b.HostName;
			IpAddress = IpUtil.GetIpFromHost(HostName).ToString();
			Service = b.Service;
			Port = (int) b.Port;
			MacAddress = b.MacAddress;
			MacAddressString = b.MacAddressName;
			Id = MacAddressString;
			if (Id != null) Name = "Lifx - " + Id.Substring(0, 4);
		}

		[JsonProperty] public string HostName { get; internal set; }
        
		[JsonProperty] public byte Service { get; internal set; }
		[JsonProperty] public int Port { get; internal set; }
		[JsonProperty] internal DateTime LastSeen { get; set; }
		[JsonProperty] public byte[] MacAddress { get; internal set; }

		[JsonProperty] public string MacAddressString { get; internal set; }

		[JsonProperty] public ushort Hue { get; set; }
		[JsonProperty] public ushort Saturation { get; set; }
		[JsonProperty] public ushort Kelvin { get; set; }
		[JsonProperty] public bool Power { get; set; }
		[DefaultValue(-1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int TargetSector { get; set; }
        
		[DefaultValue(-1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int TargetSectorV2 { get; set; }
        
		[DefaultValue(100)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int MaxBrightness { get; set; }

	}
}