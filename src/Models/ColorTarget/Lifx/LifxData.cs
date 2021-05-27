using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Glimmr.Models.Util;
using LifxNetPlus;
using LiteDB;
using Newtonsoft.Json;
using Serilog;

namespace Glimmr.Models.ColorTarget.Lifx {
	public class LifxData : IColorTargetData {
		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool HasMultiZone { get; set; }

		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool MultiZoneV2 { get; set; }

		[JsonProperty] public bool Power { get; set; }
		public bool ReverseStrip { get; set; }

		[JsonProperty] public byte Service { get; internal set; }
		[JsonProperty] public byte[] MacAddress { get; internal set; }

		[DefaultValue(82)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int LedCount {
			get => MultiZoneCount * 2;
			set => MultiZoneCount = value / 2;
		}

		[DefaultValue(100)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int MaxBrightness { get; set; } = 255;

		[DefaultValue(8)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int MultiZoneCount { get; set; }

		[JsonProperty] public int Offset { get; set; }
		[JsonProperty] public int Port { get; internal set; }

		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int ProductId { get; set; }

		[DefaultValue(-1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int TargetSector { get; set; } = -1;

		[JsonProperty] public string DeviceTag { get; internal set; }
		[BsonCtor] [JsonProperty] public string HostName { get; internal set; }
		[JsonProperty] public string MacAddressString { get; internal set; }

		[JsonProperty] public TileLayout Layout { get; set; }
		[JsonProperty] public ushort Hue { get; set; }
		[JsonProperty] public ushort Kelvin { get; set; }
		[JsonProperty] public ushort Saturation { get; set; }
		
		[JsonProperty] public BeamLayout BeamLayout { get; set; }

		private SettingsProperty[] _keyProperties;


		public LifxData() {
			Tag = "Lifx";
			if (Id == null && MacAddressString != null) {
				Id = MacAddressString;
			}

			Name ??= Tag;
			if (Id != null && Id.Length > 4) {
				Name = "Lifx - " + Id.Substring(Id.Length - 5, 5);
			}
		}

		public LifxData(LightBulb b) {
			if (b == null) {
				throw new ArgumentException("Invalid bulb data.");
			}

			Tag = "Lifx";
			Name ??= Tag;
			HostName = b.HostName;
			IpAddress = IpUtil.GetIpFromHost(HostName).ToString();
			Service = b.Service;
			Port = (int) b.Port;
			MacAddress = b.MacAddress;
			MacAddressString = b.MacAddressName;
			Id = MacAddressString;
			if (Id != null) {
				Name = "Lifx - " + Id.Substring(Id.Length - 5, 5);
			}
		}

		public string LastSeen { get; set; }


		public void UpdateFromDiscovered(IColorTargetData data) {
			var ld = (LifxData) data;
			IpAddress = data.IpAddress;
			Layout?.MergeLayout(ld.Layout);
			var omz = MultiZoneCount;
			MultiZoneCount = ld.MultiZoneCount;
			HasMultiZone = ld.HasMultiZone;
			Log.Debug($"Has multi, omz, mzc {HasMultiZone}, {omz}, {MultiZoneCount}");
			if (HasMultiZone && (omz != MultiZoneCount || BeamLayout == null)) {
				Log.Debug("Generating beam layout.");
				GenerateBeamLayout();
			}
			Log.Debug("ZOnecount is + " + MultiZoneCount);
			HostName = ld.HostName;
			IpAddress = ld.IpAddress;
			MacAddress = ld.MacAddress;
			DeviceTag = ld.DeviceTag;
			Name = "Lifx - " + Id.Substring(Id.Length - 5, 5);
		}

		public void GenerateBeamLayout() {
			Log.Debug("Generating new beam layout.");
			var total = 0;
			var beamCount = 0;
			var cornerCount = 0;
			
			for (var i = 0; i < MultiZoneCount; i++) {
				if (total == 10) {
					beamCount++;
					total = 0;
				}

				var remainder = MultiZoneCount - beamCount * 10;
				if (remainder < 10) {
					cornerCount = remainder;
				}
				total++;
			}

			BeamLayout = new BeamLayout();
			var offset = 0;
			total = 0;
			for (var i = 0; i < beamCount; i++) {
				BeamLayout.Segments.Add(new Segment(total,10,offset));
				total++;
				offset += 20;
			}
			for (var i = 0; i < cornerCount; i++) {
				BeamLayout.Segments.Add(new Segment(total,1, offset));
				total++;
				offset += 2;
			}
		}

		public SettingsProperty[] KeyProperties {
			get => Kps();
			set => _keyProperties = value;
		}


		public string Name { get; set; }
		public string Id { get; set; }
		public string Tag { get; set; }
		public string IpAddress { get; set; }
		public int Brightness { get; set; }
		
		public bool Enable { get; set; }

		private SettingsProperty[] Kps() {
			if (HasMultiZone) {
				return new SettingsProperty[] {
					new("beamMap", "beamMap", "")
				};
			}

			return new SettingsProperty[] {
				new("TargetSector", "sectormap", "Target Sector")
			};
		}
	}
	
	[Serializable]
	public class BeamLayout {
		[JsonProperty]
		public List<Segment> Segments { get; set; }
		
		public BeamLayout() {
			Segments = new List<Segment>();
		}
	}

	[Serializable]
	public class Segment {
		[JsonProperty]
		public int Position { get; set; }
		[JsonProperty]
		public int LedCount { get; set; }

		[JsonProperty]
		public int Offset { get; set; }
		
		[JsonProperty]
		public int Id { get; set; }
		
		[JsonProperty]
		public bool Reverse { get; set; }
		
		[JsonProperty]
		public bool Repeat { get; set; }

		public Segment(int position, int ledCount = 10, int offset = 0) {
			Position = position;
			Offset = offset;
			LedCount = ledCount;
			Reverse = false;
			Repeat = false;
		}
	}
}