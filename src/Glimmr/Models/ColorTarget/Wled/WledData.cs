#region

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Net;
using Glimmr.Enums;
using Glimmr.Models.Util;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Wled {
	[Serializable]
	public class WledData : IColorTargetData {
		
		/// <summary>
		/// Reverse the order of data sent to leds.
		/// </summary>
		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool ReverseStrip { get; set; }
		
		/// <summary>
		/// Device brightness.
		/// </summary>
		[JsonProperty] public int Brightness { get; set; }

		/// <summary>
		/// Number of LEDs in strip.
		/// </summary>
		[JsonProperty] public int LedCount { get; set; }
		
		/// <summary>
		/// Scale factor for LED counts related to master grid.
		/// </summary>
		[JsonProperty] public float LedMultiplier { get; set; } = 1.0f;

		/// <summary>
		/// Offset of leds from lower-right corner of master grid.
		/// </summary>
		[JsonProperty] public int Offset { get; set; }

		/// <summary>
		/// LED Strip mode.
		/// Normal = 0,
		/// Sectored = 1,
		/// Loop = 2,
		/// Single = 3
		/// </summary>
		[JsonProperty] public StripMode StripMode { get; set; }

		/// <summary>
		/// Target sector, if using sectored StripMode.
		/// </summary>
		[DefaultValue(-1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int TargetSector { get; set; }
		
		/// <summary>
		/// Device tag.
		/// </summary>
		[JsonProperty] public string Tag { get; set; }
		
		/// <summary>
		/// Device state.
		/// </summary>
		[JsonProperty] public WledStateData State { get; set; }
		
		/// <summary>
		/// Device protocol.
		/// </summary>

		[DefaultValue(2)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int Protocol { get; set; } = 2;


		public WledData() {
			Tag = "Wled";
			Name ??= Tag;
			Segments = Array.Empty<WledSegment>();
			Id = "";
			IpAddress = "";
			if (!string.IsNullOrEmpty(Id)) {
				Name = StringUtil.UppercaseFirst(Id);
			}

			LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
		}

		public WledData(string id, string ipaddress) {
			Id = id;
			Tag = "Wled";
			Name ??= Tag;
			LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
			IpAddress = ipaddress;
			Segments = Array.Empty<WledSegment>();
			using var webClient = new WebClient();
			try {
				var url = "http://" + IpAddress + "/json";
				var jsonData = webClient.DownloadString(url);
				var jsonObj = JsonConvert.DeserializeObject<WledStateData>(jsonData);
				State = jsonObj;

				LedCount = jsonObj.Info.Leds.Count;
				Segments = jsonObj.WledState.Segments;
				foreach (var seg in Segments) {
					if (seg.Offset == 0) {
						seg.Offset = seg.Start;
					}
				}
				Brightness = (int) (jsonObj.WledState.Bri / 255f * 100);
				
			} catch (Exception e) {
				Log.Debug("Yeah, here's your problem, smart guy: " + e.Message);
			}
		}

		/// <summary>
		/// Device name.
		/// </summary>
		[JsonProperty] public string Name { get; set; }
		
		/// <summary>
		/// Device ID.
		/// </summary>
		[JsonProperty] public string Id { get; set; }
		
		/// <summary>
		/// Device IP Address.
		/// </summary>
		[JsonProperty] public string IpAddress { get; set; }
		
		/// <summary>
		/// Enable streaming.
		/// </summary>
		[JsonProperty] public bool Enable { get; set; }
		
		/// <summary>
		/// Last time the device was seen during discovery.
		/// </summary>
		[JsonProperty] public string LastSeen { get; set; }
		
		/// <summary>
		/// List of individual LED segments defined in WLED.
		/// </summary>
		[JsonProperty] public WledSegment[] Segments { get; set; }


		public void UpdateFromDiscovered(IColorTargetData data) {
			var input = (WledData) data;
			if (input == null) {
				throw new ArgumentNullException(nameof(data));
			}

			LedCount = input.LedCount;
			IpAddress = data.IpAddress;
			Name = StringUtil.UppercaseFirst(input.Name);
			var segments = input.Segments;
			for (var i = 0; i < segments.Length; i++) {
				if (Segments.Length < i) {
					segments[i].Offset = Segments[i].Offset;
				}
			}
			Segments = segments;
		}

		/// <summary>
		/// UI Properties.
		/// </summary>
		public SettingsProperty[] KeyProperties {
			get => Kps();
			set { }
		}

		private SettingsProperty[] Kps() {
			var multiplier = new SettingsProperty("LedMultiplier", "ledMultiplier", "");
			if (StripMode == Enums.StripMode.Single) {
				return new[] {
					new("sectormap", "sectormap", ""),
					new("StripMode", "select", "Strip Mode", new Dictionary<string, string> {
						["0"] = "Normal",
						["1"] = "Sectored",
						["2"] = "Loop (Play Bar)",
						["3"] = "Single Color"
					}),
					new("LedCount", "number", "Led Count"),
					multiplier,
					new("ReverseStrip", "check", "Reverse Strip Direction"),
					new("Protocol", "select", "Streaming Protocol", new Dictionary<string, string> {
						["1"] = "WARLS",
						["2"] = "DRGB",
						["3"] = "DRGBW",
						["4"] = "DNRGB"
					}){ValueHint = "Select desired WLED protocol. Default is DRGB."}
				};
			}
			
			if (StripMode == Enums.StripMode.Sectored) {
				var props = new List<SettingsProperty> {
					new("sectorLedMap", "sectorLedMap", ""),
					new("StripMode", "select", "Strip Mode", new Dictionary<string, string> {
						["0"] = "Normal",
						["1"] = "Sectored",
						["2"] = "Loop (Play Bar)",
						["3"] = "Single Color"
					}),
					new("Protocol", "select", "Streaming Protocol", new Dictionary<string, string> {
						["1"] = "WARLS",
						["2"] = "DRGB",
						["3"] = "DRGBW",
						["4"] = "DNRGB"
					}){ValueHint = "Select desired WLED protocol. Default is DRGB."}
				};
				foreach (var seg in Segments) {
					var id = seg.Id;
					props.Add(new SettingsProperty($"segmentOffset{id}","number",$"Segment {id} Offset"));
				}

				return props.ToArray();
			}

			return new[] {
				new("ledmap", "ledmap", ""),
				new("StripMode", "select", "Strip Mode", new Dictionary<string, string> {
					["0"] = "Normal",
					["1"] = "Sectored",
					["2"] = "Loop (Play Bar)",
					["3"] = "Single Color"
				}),
				new("LedCount", "number", "Led Count"),
				new("Offset", "number", "Strip Offset"),
				multiplier,
				new("ReverseStrip", "check", "Reverse Strip")
					{ValueHint = "Reverse the order of the leds to clockwise (facing screen)."},
				new("Protocol", "select", "Streaming Protocol", new Dictionary<string, string> {
					["1"] = "WARLS",
					["2"] = "DRGB",
					["3"] = "DRGBW",
					["4"] = "DNRGB"
				}){ValueHint = "Select desired WLED protocol. Default is DRGB."}
			};
		}
	}

	/// <summary>
	/// 
	/// </summary>
	public struct Ccnf {
		[JsonProperty("max")] public int Max { get; set; }
		[JsonProperty("min")] public int Min { get; set; }
		[JsonProperty("time")] public int Time { get; set; }
	}

	/// <summary>
	/// Nightlight configuration
	/// </summary>
	public class Nl {
		/// <summary>
		/// Dim over course of nightlight duration.
		/// </summary>
		[JsonProperty("fade")] public bool Fade { get; set; }
		
		/// <summary>
		/// Enable nightlight mode.
		/// </summary>

		[JsonProperty("on")] public bool On { get; set; }
		
		/// <summary>
		/// NightLight duration.
		/// </summary>

		[JsonProperty("dur")] public int Duration { get; set; }
		
		/// <summary>
		/// Nightlight mode (0: instant, 1: fade, 2: color fade, 3: sunrise).
		/// </summary>
		[JsonProperty("mode")] public int Mode { get; set; }

		/// <summary>
		/// Target brightness of nightlight feature.
		/// </summary>
		[JsonProperty("tbri")] public int Tbri { get; set; }
	}

	public class Udpn {
		/// <summary>
		/// Receive broadcast packets
		/// </summary>
		[JsonProperty("recv")] public bool Recv { get; set; }
		
		/// <summary>
		/// Send WLED broadcast (UDP sync) packet on state change
		/// </summary>

		[JsonProperty("send")] public bool Send { get; set; }
	}

	public class WledSegment {
		/// <summary>
		/// Mirror the segment.
		/// </summary>
		[JsonProperty("mi")] public bool Mi { get; set; }
		
		/// <summary>
		/// Segment is enabled.
		/// </summary>

		[JsonProperty("on")] public bool On { get; set; }

		/// <summary>
		/// Flip the segment (reverse animation)
		/// </summary>
		[JsonProperty("rev")] public bool ReverseStrip { get; set; }
		
		/// <summary>
		/// True if segment is selected.
		/// </summary>
		[JsonProperty("sel")] public bool Sel { get; set; }

		/// <summary>
		/// Segment brightness.
		/// </summary>
		[JsonProperty("bri")] public int Brightness { get; set; }

		/// <summary>
		/// ID of segment effect.
		/// </summary>
		[JsonProperty("fx")] public int Fx { get; set; }

		/// <summary>
		/// Segment group
		/// </summary>
		[JsonProperty("grp")] public int Grp { get; set; }

		/// <summary>
		/// Segment ID.
		/// </summary>
		[JsonProperty("id")] public int Id { get; set; }
		
		/// <summary>
		/// Effect intensity.
		/// </summary>
		[JsonProperty("ix")] public int Ix { get; set; }

		/// <summary>
		/// Segment length.
		/// </summary>
		[JsonProperty("len")] public int LedCount { get; set; }

		/// <summary>
		/// ID of the color palette.
		/// </summary>
		[JsonProperty("pal")] public int Pal { get; set; }
		
		
		/// <summary>
		/// Spacing?
		/// </summary>
		[JsonProperty("spc")] public int Spc { get; set; }

		/// <summary>
		/// Segment start position.
		/// </summary>
		[JsonProperty("start")] public int Start { get; set; }
		
		/// <summary>
		/// Segment offset in reference to Glimmr master Grid.
		/// </summary>
		[JsonProperty("Offset")] public int Offset { get; set; }
		
		/// <summary>
		/// Scale factor for LED counts related to master grid.
		/// </summary>
		[JsonProperty("LedMultiplier")] public float Multiplier { get; set; }
		
		/// <summary>
		/// Segment end position.
		/// </summary>
		[JsonProperty("stop")] public int Stop { get; set; }

		/// <summary>
		/// Relative effect speed.
		/// </summary>
		[JsonProperty("sx")] public int Sx { get; set; }

		/// <summary>
		/// Segment colors
		/// </summary>
		[JsonProperty("col")] public List<List<int>>? Col { get; set; }
	}

	public struct WledState {
		/// <summary>
		/// Is the device on?
		/// </summary>
		[JsonProperty("on")] public bool On { get; set; }
		
		/// <summary>
		/// Missing?
		/// </summary>
		[JsonProperty("ccnf")] public Ccnf Ccnf { get; set; }
		
		/// <summary>
		/// WLED Brightness.
		/// </summary>
		[JsonProperty("bri")] public int Bri { get; set; }
		
		/// <summary>
		/// Live Data Override.
		/// </summary>
		[JsonProperty("lor")] public int Lor { get; set; }
		
		/// <summary>
		/// Main Segment.
		/// </summary>
		[JsonProperty("mainseg")] public int Mainseg { get; set; }
		
		/// <summary>
		/// ID of currently set playlist.
		/// </summary>
		[JsonProperty("pl")] public int Pl { get; set; }
		
		/// <summary>
		/// Id of currently set preset.
		/// </summary>
		[JsonProperty("ps")] public int Ps { get; set; }
		
		/// <summary>
		/// Bitwise indication of preset slots.
		/// </summary>
		[JsonProperty("pss")] public int Pss { get; set; }
		
		/// <summary>
		/// Duration of cross fade between different colors.
		/// </summary>
		[JsonProperty("transition")] public int Transition { get; set; }
		
		/// <summary>
		/// Individual segments.
		/// </summary>
		[JsonProperty("seg")] public WledSegment[] Segments { get; set; }
		
		/// <summary>
		/// Night light settings.
		/// </summary>
		[JsonProperty("nl")] public Nl Nl { get; set; }
		
		/// <summary>
		/// UDP settings.
		/// </summary>
		[JsonProperty("udpn")] public Udpn Udpn { get; set; }
	}

	public struct Leds {
		/// <summary>
		/// Is the strip RGB+W?
		/// </summary>
		[JsonProperty("rgbw")] public bool Rgbw { get; set; }
		
		/// <summary>
		/// Is the segment locked.
		/// </summary>
		[JsonProperty("seglock")] public bool Seglock { get; set; }
		
		/// <summary>
		/// Show white channel slider.
		/// </summary>
		[JsonProperty("wv")] public bool Wv { get; set; }
		
		/// <summary>
		/// Number of LEDs.
		/// </summary>
		[JsonProperty("count")] public int Count { get; set; }
		
		/// <summary>
		/// Maximum power.
		/// </summary>
		[JsonProperty("maxpwr")] public int Maxpwr { get; set; }
		
		/// <summary>
		/// Maximum number of segments.
		/// </summary>
		[JsonProperty("maxseg")] public int Maxseg { get; set; }
		
		/// <summary>
		/// Current LED power usage.
		/// </summary>
		[JsonProperty("pwr")] public int Pwr { get; set; }
		
		/// <summary>
		/// LED data pin.
		/// </summary>
		[JsonProperty("pin")] public List<int> Pin { get; set; }
	}

	public struct Wifi {
		/// <summary>
		/// Wifi channel.
		/// </summary>
		[JsonProperty("channel")] public int Channel { get; set; }
		
		/// <summary>
		/// Wifi RSSI.
		/// </summary>
		[JsonProperty("rssi")] public int Rssi { get; set; }
		
		/// <summary>
		/// Wifi signal strength.
		/// </summary>
		[JsonProperty("signal")] public int Signal { get; set; }
		
		/// <summary>
		/// Wifi bssid.
		/// </summary>
		[JsonProperty("bssid")] public string Bssid { get; set; }
	}

	public struct Info {
		/// <summary>
		/// Is live streaming enabled?
		/// </summary>
		[JsonProperty("live")] public bool Live { get; set; }
		/// <summary>
		/// UI Sync button options.
		/// </summary>
		[JsonProperty("str")] public bool Str { get; set; }
		
		/// <summary>
		/// Free memory heap.
		/// </summary>
		[JsonProperty("freeheap")] public int Freeheap { get; set; }
		
		/// <summary>
		/// Number of included effects.
		/// </summary>
		[JsonProperty("fxcount")] public int Fxcount { get; set; }
		
		/// <summary>
		/// IP address?
		/// </summary>
		[JsonProperty("lwip")] public int Lwip { get; set; }
		
		/// <summary>
		/// Debugging only.
		/// </summary>
		[JsonProperty("opt")] public int Opt { get; set; }
		
		/// <summary>
		/// Palette count.
		/// </summary>
		[JsonProperty("palcount")] public int Palcount { get; set; }
		
		/// <summary>
		/// Udp port.
		/// </summary>
		[JsonProperty("udpport")] public int Udpport { get; set; }
		
		/// <summary>
		/// System Uptime.
		/// </summary>
		[JsonProperty("uptime")] public int Uptime { get; set; }
		
		/// <summary>
		/// Build ID.
		/// </summary>
		[JsonProperty("vid")] public int Vid { get; set; }
		
		/// <summary>
		/// Number of current websocket clients.
		/// </summary>
		[JsonProperty("ws")] public int Ws { get; set; }
		
		/// <summary>
		/// LED Data.
		/// </summary>
		[JsonProperty("leds")] public Leds Leds { get; set; }
		
		/// <summary>
		/// Architecture.
		/// </summary>
		[JsonProperty("arch")] public string Arch { get; set; }
		
		/// <summary>
		/// Device brand.
		/// </summary>
		[JsonProperty("brand")] public string Brand { get; set; }
		
		/// <summary>
		/// Arduino core version.
		/// </summary>
		[JsonProperty("core")] public string Core { get; set; }
		
		/// <summary>
		/// LED IP.
		/// </summary>
		[JsonProperty("lip")] public string Lip { get; set; }
		
		/// <summary>
		/// Realtime datasource info.
		/// </summary>
		[JsonProperty("lm")] public string Lm { get; set; }
		
		/// <summary>
		/// Device MAC address.
		/// </summary>
		[JsonProperty("mac")] public string Mac { get; set; }
		
		/// <summary>
		/// Device name.
		/// </summary>
		[JsonProperty("name")] public string Name { get; set; }
		
		/// <summary>
		/// Product type.
		/// </summary>
		[JsonProperty("product")] public string Product { get; set; }
		
		/// <summary>
		/// Product version.
		/// </summary>
		[JsonProperty("ver")] public string Ver { get; set; }
		
		/// <summary>
		/// Wifi info.
		/// </summary>
		[JsonProperty("wifi")] public Wifi Wifi { get; set; }
	}

	public struct WledStateData {
		/// <summary>
		/// State info.
		/// </summary>
		[JsonProperty("info")] public Info Info { get; set; }
		/// <summary>
		/// WLED State.
		/// </summary>
		[JsonProperty("state")] public WledState WledState { get; set; }
	}
}