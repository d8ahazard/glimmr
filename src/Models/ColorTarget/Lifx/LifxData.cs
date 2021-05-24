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
			if (HasMultiZone && (omz != MultiZoneCount || BeamLayout == null)) {
				GenerateBeamLayout();
			}
			Log.Debug("ZOnecount is + " + MultiZoneCount);
			HostName = ld.HostName;
			IpAddress = ld.IpAddress;
			MacAddress = ld.MacAddress;
			DeviceTag = ld.DeviceTag;
			Name = "Lifx - " + Id.Substring(Id.Length - 5, 5);
		}

		private void GenerateBeamLayout() {
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
			
			var curBeams = new List<Beam>();
			var curCorners = new List<Corner>();
			if (BeamLayout != null) {
				curBeams = BeamLayout.Beams;
				curCorners = BeamLayout.Corners;
			}
			
			BeamLayout = new BeamLayout();

			var posCount = 0;
			for (var i = 0; i < beamCount; i++) {
				var beam = new Beam(i);
				foreach (var b in curBeams) {
					if (b.Position == i) {
						beam = b;
					}		
				}

				var skip = false;
				foreach (var c in curCorners.Where(c => c.Position == i)) {
					skip = true;
					BeamLayout.Corners.Add(c);
				}
				if (!skip) BeamLayout.Beams.Add(beam);
				posCount++;
			}

			for (var c = 0; c < cornerCount; c++) {
				var corner = new Corner(posCount, beamCount * 10);
				var skip = false;
				foreach (var ec in curCorners.Where(ec => ec.Position == c)) {
					skip = true;
					BeamLayout.Corners.Add(ec);
				}
				
				foreach (var b in curBeams.Where(b => b.Position == c)) {
					skip = true;
				}

				if (!skip) BeamLayout.Corners.Add(corner);

				posCount++;
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
					new("ledmap", "ledmap", ""),
					new("Offset", "number", "Offset"),
					new("ReverseStrip", "check", "Reverse Direction")
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
		public List<Beam> Beams { get; set; }
		[JsonProperty]
		public List<Corner> Corners { get; set; }

		public BeamLayout() {
			Beams = new List<Beam>();
			Corners = new List<Corner>();
		}
	}

	[Serializable]
	public class Beam {
		[JsonProperty]
		public int Orientation { get; set; }
		[JsonProperty]
		public int Position { get; set; }
		[JsonProperty]
		public int LedCount { get; set; } = 10;

		[JsonProperty]
		public int Offset { get; set; }
		
		[JsonProperty]
		public bool Reverse { get; set; }
		
		[JsonProperty]
		public bool Repeat { get; set; }

		public Beam(int position, int orientation = 0) {
			Position = position;
			Orientation = orientation;
			Offset = position * 10;
			Reverse = false;
			Repeat = false;
		}
	}

	[Serializable]
	public class Corner {
		[JsonProperty]
		public int Orientation { get; set; }
		[JsonProperty]
		public int Position { get; set; }
		[JsonProperty]
		public int LedCount { get; set; } = 1;
		
		[JsonProperty]
		public int Offset { get; set; }

		public Corner(int position, int offset, int orientation = 0) {
			Position = position;
			Orientation = orientation;
			Offset = offset;
		}
	}

}