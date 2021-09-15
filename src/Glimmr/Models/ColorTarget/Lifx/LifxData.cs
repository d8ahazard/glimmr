#region

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Glimmr.Models.Util;
using LifxNetPlus;
using LiteDB;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget.Lifx {
	public class LifxData : IColorTargetData {
		[JsonProperty] public BeamLayout BeamLayout { get; set; }

		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool HasMultiZone { get; set; }

		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool MultiZoneV2 { get; set; }

		[JsonProperty] public bool Power { get; set; }
		[JsonProperty] public byte Service { get; internal set; }
		[JsonProperty] public byte[] MacAddress { get; internal set; } = new byte[0];
		[JsonProperty] public double GammaCorrection { get; set; } = 1;
		[JsonProperty] public int Brightness { get; set; }

		[DefaultValue(82)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int LedCount {
			get => MultiZoneCount * 2;
			set => MultiZoneCount = value / 2;
		}

		[JsonProperty] public float LedMultiplier { get; set; } = 2.0f;

		[DefaultValue(255)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int MaxBrightness { get; set; } = 255;

		[DefaultValue(8)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int MultiZoneCount { get; set; }

		[JsonProperty] public int Port { get; internal set; }

		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int ProductId { get; set; }

		[DefaultValue(-1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int TargetSector { get; set; } = -1;

		[JsonProperty] public string DeviceTag { get; internal set; }
		[BsonCtor] [JsonProperty] public string HostName { get; internal set; } = "";
		[JsonProperty] public string MacAddressString { get; internal set; } = "";
		[JsonProperty] public string Tag { get; set; }

		[JsonProperty] public TileLayout Layout { get; set; }
		[JsonProperty] public ushort Hue { get; set; }
		[JsonProperty] public ushort Kelvin { get; set; }
		[JsonProperty] public ushort Saturation { get; set; }


		public LifxData() {
			Tag = "Lifx";
			Layout = new TileLayout();
			BeamLayout = new BeamLayout();
			DeviceTag = "Lifx Bulb";
			LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
			Brightness = 255;
		}

		public LifxData(Device b) {
			Tag = "Lifx";
			Name ??= Tag;
			Id = HostName;
			HostName = b.HostName;
			var ip = IpUtil.GetIpFromHost(HostName);
			if (ip != null) {
				IpAddress = ip.ToString();
			}

			if (Id == null && MacAddressString != null) {
				Id = MacAddressString;
			}

			Name ??= Tag;
			if (Id != null && Id.Length > 4) {
				Name = "Lifx - " + Id.Substring(Id.Length - 5, 5);
			}

			Service = b.Service;
			Port = (int) b.Port;
			MacAddress = b.MacAddress;
			MacAddressString = b.MacAddressName;
			Id = MacAddressString;
			BeamLayout = new BeamLayout();
			Layout = new TileLayout();
			DeviceTag = "Lifx Bulb";
			Brightness = 255;
			Name = "Lifx - " + Id.Substring(Id.Length - 5, 5);
			LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
			Kps();
		}

		[JsonProperty] public string Name { get; set; } = "";
		[JsonProperty] public string Id { get; set; } = "";
		[JsonProperty] public string IpAddress { get; set; } = "";
		[JsonProperty] public bool Enable { get; set; }
		[JsonProperty] public string LastSeen { get; set; }


		public void UpdateFromDiscovered(IColorTargetData data) {
			var ld = (LifxData) data;
			HostName = ld.HostName;
			IpAddress = ld.IpAddress;
			MacAddress = ld.MacAddress;
			DeviceTag = ld.DeviceTag;
			Name = DeviceTag + " - " + Id.Substring(Id.Length - 5, 5);
			Layout.MergeLayout(ld.Layout);
			var omz = MultiZoneCount;
			MultiZoneCount = ld.MultiZoneCount;
			HasMultiZone = ld.HasMultiZone;
			if (HasMultiZone && (omz != MultiZoneCount || BeamLayout == null)) {
				GenerateBeamLayout();
			}
		}

		public SettingsProperty[] KeyProperties {
			get => Kps();
			set { }
		}


		public void GenerateBeamLayout() {
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
				BeamLayout.Segments.Add(new Segment(total, 10, offset));
				total++;
				offset += 20;
			}

			for (var i = 0; i < cornerCount; i++) {
				BeamLayout.Segments.Add(new Segment(total, 1, offset));
				total++;
				offset += 2;
			}
		}

		private SettingsProperty[] Kps() {
			if (HasMultiZone) {
				var gamma = new SettingsProperty("GammaCorrection", "number", "Gamma Correction") {
					ValueMax = "3", ValueMin = "1", ValueStep = ".1"
				};
				return new[] {
					new("LedMultiplier", "ledMultiplier", ""),
					gamma,
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
		[JsonProperty] public List<Segment> Segments { get; set; }

		public BeamLayout() {
			Segments = new List<Segment>();
		}
	}

	[Serializable]
	public class Segment {
		[JsonProperty] public bool Repeat { get; set; }

		[JsonProperty] public bool Reverse { get; set; }

		[JsonProperty] public int Id { get; set; }
		[JsonProperty] public int LedCount { get; set; }

		[JsonProperty] public int Offset { get; set; }
		[JsonProperty] public int Position { get; set; }

		public Segment(int position, int ledCount = 10, int offset = 0) {
			Position = position;
			Offset = offset;
			LedCount = ledCount;
			Reverse = false;
			Repeat = false;
		}
	}
}