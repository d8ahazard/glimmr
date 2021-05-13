using System;
using System.ComponentModel;
using Glimmr.Models.Util;
using LifxNetPlus;
using LiteDB;
using Native;
using Newtonsoft.Json;
using Serilog;

namespace Glimmr.Models.ColorTarget.Lifx {
	public class LifxData : IColorTargetData {
		[BsonCtor] [JsonProperty] public string HostName { get; internal set; }
        
		[JsonProperty] public byte Service { get; internal set; }
		[JsonProperty] public int Port { get; internal set; }
		[JsonProperty] public byte[] MacAddress { get; internal set; }
		[JsonProperty] public string DeviceTag { get; internal set; }
		[JsonProperty] public string MacAddressString { get; internal set; }

		[JsonProperty] public int Offset { get; set; }
		[JsonProperty] public ushort Hue { get; set; }
		[JsonProperty] public ushort Saturation { get; set; }
		[JsonProperty] public ushort Kelvin { get; set; }
		[JsonProperty] public bool Power { get; set; }

		[DefaultValue(-1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int TargetSector { get; set; } = -1;

		[DefaultValue(100)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int MaxBrightness { get; set; } = 255;
		
		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool HasMultiZone { get; set; }
		
		[DefaultValue(8)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int MultiZoneCount { get; set; }
		
		[DefaultValue(82)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int LedCount {
			get => MultiZoneCount;
			set => MultiZoneCount = value;
		}
		
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int ProductId { get; set; }
		
		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool MultiZoneV2 { get; set; }
		
		[JsonProperty] public TileLayout Layout { get; set; }
		
		
		public LifxData() {
			Tag = "Lifx";
			if (Id == null && MacAddressString != null) {
				Id = MacAddressString;
			}
			Name ??= Tag;
			if (Id != null && Id.Length > 4) Name = "Lifx - " + Id.Substring(0, 4);
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

		public string LastSeen { get; set; }

		
		public void UpdateFromDiscovered(IColorTargetData data) {
			var ld = (LifxData) data;
			IpAddress = data.IpAddress;
			Layout?.MergeLayout(ld.Layout);
			MultiZoneCount = ld.MultiZoneCount;
			HasMultiZone = ld.HasMultiZone;
			HostName = ld.HostName;
			IpAddress = ld.IpAddress;
			MacAddress = ld.MacAddress;
			DeviceTag = ld.DeviceTag;
		}

		public SettingsProperty[] KeyProperties {
			get => Kps();
			set => _keyProperties = value;
		}

		private SettingsProperty[] _keyProperties;

		private SettingsProperty[] Kps() {
			if (HasMultiZone) {
				return new SettingsProperty[]{
					new("ledmap","ledmap",""),
					new("Offset", "number", "Offset"),
					new("Reverse Direction", "check", "ReverseStrip"),
					new("FrameDelay", "text", "Frame Delay")
				};
			}

			return new SettingsProperty[] {
				new("TargetSector", "sectormap", "Target Sector"),
				new("FrameDelay", "text", "Frame Delay")
			};
		}


		public string Name { get; set; }
		public string Id { get; set; }
		public string Tag { get; set; }
		public string IpAddress { get; set; }
		public int Brightness { get; set; }
		public int FrameDelay { get; set; }
		public bool Enable { get; set; }
		public bool ReverseStrip { get; set; }
	}
}