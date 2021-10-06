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
		/// <summary>
		/// Layout of beam elements.
		/// </summary>
		[JsonProperty] public BeamLayout BeamLayout { get; set; }

		/// <summary>
		/// If this device supports multi-zone operations.
		/// </summary>
		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool HasMultiZone { get; set; }

		/// <summary>
		/// Supports V2 Multi-zone operations.
		/// </summary>
		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool MultiZoneV2 { get; set; }

		/// <summary>
		/// Is the device on?
		/// </summary>
		[JsonProperty] public bool Power { get; set; }
		
		/// <summary>
		/// 
		/// </summary>
		[JsonProperty] public byte Service { get; internal set; }
		
		/// <summary>
		/// Device's MAC Address.
		/// </summary>
		[JsonProperty] public byte[] MacAddress { get; internal set; } = Array.Empty<byte>();
		
		/// <summary>
		/// Gamma Correction level.
		/// </summary>
		[JsonProperty] public double GammaCorrection { get; set; } = 1;
		
		/// <summary>
		/// Device Brightness.
		/// </summary>
		[JsonProperty] public int Brightness { get; set; }

		/// <summary>
		/// UI Properties.
		/// </summary>
		[DefaultValue(82)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int LedCount {
			get => MultiZoneCount * 2;
			set => MultiZoneCount = value / 2;
		}
		
		/// <summary>
		/// Scale factor for LED counts related to master grid.
		/// </summary>
		[JsonProperty] public float LedMultiplier { get; set; } = 2.0f;

		/// <summary>
		/// Number of zones, if device has multi-zone support.
		/// </summary>
		[DefaultValue(8)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int MultiZoneCount { get; set; }

		/// <summary>
		/// Port used for communication.
		/// </summary>
		[JsonProperty] public int Port { get; internal set; }

		/// <summary>
		/// Product ID.
		/// </summary>
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int ProductId { get; set; }

		/// <summary>
		/// Selected sector, if device is single-color.
		/// </summary>
		[DefaultValue(-1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int TargetSector { get; set; } = -1;

		/// <summary>
		/// Lifx-Specific Device Tag.
		/// </summary>
		[JsonProperty] public string DeviceTag { get; internal set; }
		
		/// <summary>
		/// Device Hostname.
		/// </summary>
		[BsonCtor] [JsonProperty] public string HostName { get; internal set; } = "";
		
		/// <summary>
		/// Device MAC Address.
		/// </summary>
		[JsonProperty] public string MacAddressString { get; internal set; } = "";
		
		/// <summary>
		/// Device Tag
		/// </summary>
		[JsonProperty] public string Tag { get; set; }

		/// <summary>
		/// Tile Layout, if device is Lifx Tile
		/// </summary>
		[JsonProperty] public TileLayout Layout { get; set; }
		
		/// <summary>
		/// Device Hue - Bulb only.
		/// </summary>
		[JsonProperty] public ushort Hue { get; set; }
		
		/// <summary>
		/// Device Color Temperature - Bulb only.
		/// </summary>
		[JsonProperty] public ushort Kelvin { get; set; }
		
		/// <summary>
		/// Device Saturation - Bulb only.
		/// </summary>
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
			if (Id is { Length: > 4 }) {
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

		/// <summary>
		/// Device Name.
		/// </summary>
		[JsonProperty] public string Name { get; set; } = "";
		
		/// <summary>
		/// Device ID.
		/// </summary>

		[JsonProperty] public string Id { get; set; } = "";
		
		/// <summary>
		/// Device IP Address.
		/// </summary>

		[JsonProperty] public string IpAddress { get; set; } = "";
		
		/// <summary>
		/// Enable device for streaming.
		/// </summary>
		[JsonProperty] public bool Enable { get; set; }
		
		/// <summary>
		/// Last time the device was seen during discovery.
		/// </summary>
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

		/// <summary>
		/// UI Properties.
		/// </summary>
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
			if (!HasMultiZone) {
				return new SettingsProperty[] {
					new("TargetSector", "sectormap", "Target Sector")
				};
			}

			var gamma = new SettingsProperty("GammaCorrection", "number", "Gamma Correction") {
				ValueMax = "3", ValueMin = "1", ValueStep = ".1"
			};
			return new[] {
				new("LedMultiplier", "ledMultiplier", ""),
				gamma,
				new("beamMap", "beamMap", "")
			};

		}
	}

	[Serializable]
	public class BeamLayout {
		/// <summary>
		/// List of individual segments.
		/// </summary>
		[JsonProperty] public List<Segment> Segments { get; set; }

		public BeamLayout() {
			Segments = new List<Segment>();
		}
	}

	/// <summary>
	/// Led Beam Data
	/// </summary>
	[Serializable]
	
	public class Segment {
		/// <summary>
		/// Use one color for the whole beam.
		/// </summary>
		[JsonProperty] public bool Repeat { get; set; }
		
		/// <summary>
		/// Reverse color data order.
		/// </summary>
		[JsonProperty] public bool Reverse { get; set; }
		
		/// <summary>
		/// Beam ID. This is an arbitrary value.
		/// </summary>
		[JsonProperty] public int Id { get; set; }
		
		/// <summary>
		/// Number of leds per beam. Don't change this.
		/// </summary>
		[JsonProperty] public int LedCount { get; set; }

		/// <summary>
		/// Offset of leds from lower-right corner of master grid.
		/// </summary>
		[JsonProperty] public int Offset { get; set; }
		
		/// <summary>
		/// This beam's position in the layout.
		/// </summary>
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