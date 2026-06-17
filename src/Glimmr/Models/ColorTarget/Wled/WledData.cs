#region

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using Glimmr.Enums;
using Glimmr.Models.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Wled;

[Serializable]
public class WledData : IColorTargetData {
	/// <summary>
	///     Reverse the order of data sent to leds.
	/// </summary>
	[DefaultValue(false)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool ReverseStrip { get; set; }

	/// <summary>
	///     Scale factor for LED counts related to master grid.
	/// </summary>
	[JsonProperty]
	public float LedMultiplier { get; set; } = 1.0f;

	/// <summary>
	///     Device brightness.
	/// </summary>
	[JsonProperty]
	public int Brightness { get; set; }

	/// <summary>
	///     Number of LEDs in strip.
	/// </summary>
	[JsonProperty]
	public int LedCount { get; set; }

	/// <summary>
	///     Offset of leds from lower-right corner of master grid.
	/// </summary>
	[JsonProperty]
	public int Offset { get; set; }

	/// <summary>
	///     Device protocol.
	/// </summary>

	[DefaultValue(2)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int Protocol { get; set; } = 2;

	/// <summary>
	///     Target sector, if using sectored StripMode.
	/// </summary>
	[DefaultValue(-1)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int TargetSector { get; set; }

	/// <summary>
	///     LED Strip mode.
	///     Normal = 0,
	///     Sectored = 1,
	///     Loop = 2,
	///     Single = 3
	/// </summary>
	[JsonProperty]
	public StripMode StripMode { get; set; }

	/// <summary>
	///     List of individual LED segments defined in WLED.
	/// </summary>
	[JsonProperty]
	public WledSegment[] Segments { get; set; }

	/// <summary>
	///     Device state.
	/// </summary>
	[JsonProperty]
	public WledStateData State { get; set; }
	
	
	[JsonProperty]
	public bool Initialized { get; set; }

	/// <summary>
	///     Matrix width in pixels (auto-detected from WLED's 2D config when available).
	/// </summary>
	[JsonProperty]
	public int MatrixWidth { get; set; }

	/// <summary>
	///     Matrix height in pixels (auto-detected from WLED's 2D config when available).
	/// </summary>
	[JsonProperty]
	public int MatrixHeight { get; set; }

	/// <summary>
	///     How the matrix maps the screen: 2 = Mirror (literal), 3 = Extend (edge reflection).
	/// </summary>
	[DefaultValue(MatrixMode.Mirror)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public MatrixMode MatrixMode { get; set; } = MatrixMode.Mirror;

	/// <summary>
	///     Which screen edge the panel sits next to. 0 = Left, 1 = Right, 2 = Top, 3 = Bottom.
	///     For Extend, this is the edge that gets reflected outward across the panel.
	/// </summary>
	[DefaultValue(MatrixSide.Bottom)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public MatrixSide MatrixSide { get; set; } = MatrixSide.Bottom;

	/// <summary>
	///     Extend mode: how far into the screen (percent) the edge reflection samples.
	/// </summary>
	[DefaultValue(50)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int ExtendDepth { get; set; } = 50;

	/// <summary>
	///     Extend mode: brightness drop (percent) at the panel's far edge, so the reflection
	///     fades into the distance.
	/// </summary>
	[DefaultValue(40)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int ExtendFade { get; set; } = 40;

	/// <summary>
	///     Flip the matrix output left-right before sending (for physical mounting orientation).
	/// </summary>
	[DefaultValue(false)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool FlipHorizontal { get; set; }

	/// <summary>
	///     Flip the matrix output top-bottom before sending (for physical mounting orientation).
	/// </summary>
	[DefaultValue(false)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool FlipVertical { get; set; }

	/// <summary>
	///     Matrix wiring zig-zags (serpentine). Auto-detected from WLED's 2D panel config.
	/// </summary>
	[DefaultValue(false)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool Serpentine { get; set; }

	/// <summary>
	///     Matrix wiring runs in columns (vertical/transposed) rather than rows.
	///     Auto-detected from WLED's 2D panel config.
	/// </summary>
	[DefaultValue(false)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool Vertical { get; set; }

	/// <summary>
	///     Wiring start corner: 0 = Top-Left, 1 = Top-Right, 2 = Bottom-Left, 3 = Bottom-Right.
	///     Auto-detected from WLED's 2D panel config.
	/// </summary>
	[JsonProperty]
	public int StartCorner { get; set; }


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

		// Fetch the lightweight state+info endpoint (no effects/palettes) with a generous
		// timeout and a couple of retries. Cheap ESP32-C3 boards on weak WiFi are slow to
		// answer the full /json, which used to time out and silently drop the device.
		var jsonData = FetchStateInfo();
		if (string.IsNullOrEmpty(jsonData)) {
			Log.Debug($"WLED: No data from {IpAddress} after retries; not initializing.");
			Initialized = false;
			return;
		}

		try {
			var jsonObj = JsonConvert.DeserializeObject<WledStateData>(jsonData);
			State = jsonObj;

			LedCount = jsonObj.Info.Leds.Count;
			Segments = jsonObj.WledState.Segments ?? Array.Empty<WledSegment>();
			foreach (var seg in Segments) {
				if (seg.Offset == 0) {
					seg.Offset = seg.Start;
				}
			}

			Name = State.Info.Name;
			Brightness = (int)(jsonObj.WledState.Bri / 255f * 100);
			Initialized = true;

			// Auto-detect a 2D matrix. WLED applies its own panel wiring to realtime data, so we
			// only need the dimensions here (Glimmr streams a plain row-major image over DDP).
			var matrix = jsonObj.Info.Leds.Matrix;
			if (matrix is { W: > 0, H: > 0 }) {
				MatrixWidth = matrix.W;
				MatrixHeight = matrix.H;
				StripMode = StripMode.Matrix;
				MatrixMode = MatrixMode.Mirror;
			}
		} catch (Exception e) {
			Log.Debug("Exception parsing WLED Data: " + e.Message);
			Initialized = false;
		}
	}

	/// <summary>
	///     Fetch WLED's <c>/json/si</c> (state + info only) with retries. Returns null on failure.
	/// </summary>
	private string? FetchStateInfo() {
		const int attempts = 3;
		var url = "http://" + IpAddress + "/json/si";
		for (var i = 0; i < attempts; i++) {
			using var webClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
			try {
				return webClient.GetStringAsync(url).Result;
			} catch (Exception e) {
				Log.Debug($"WLED: fetch attempt {i + 1}/{attempts} for {IpAddress} failed: {e.Message}");
			}
		}

		return null;
	}

	/// <summary>
	///     Device tag.
	/// </summary>
	[JsonProperty]
	public string Tag { get; set; }

	/// <summary>
	///     Device name.
	/// </summary>
	[JsonProperty]
	public string Name { get; set; }

	/// <summary>
	///     Device ID.
	/// </summary>
	[JsonProperty]
	public string Id { get; set; }

	/// <summary>
	///     Device IP Address.
	/// </summary>
	[JsonProperty]
	public string IpAddress { get; set; }

	/// <summary>
	///     Enable streaming.
	/// </summary>
	[JsonProperty]
	public bool Enable { get; set; }

	/// <summary>
	///     Last time the device was seen during discovery.
	/// </summary>
	[JsonProperty]
	public string LastSeen { get; set; }


	public void UpdateFromDiscovered(IColorTargetData data) {
		var input = (WledData)data;
		if (input == null) {
			throw new ArgumentNullException(nameof(data));
		}

		LedCount = input.LedCount;
		IpAddress = data.IpAddress;
		Name = StringUtil.UppercaseFirst(input.Name);
		var segments = input.Segments;
		for (var i = 0; i < segments.Length; i++) {
			if (i < Segments.Length) {
				segments[i].Offset = Segments[i].Offset;
			}
		}

		Segments = segments;

		// Carry matrix dimensions across rediscovery. On first detection switch to Matrix mode;
		// user mode/side/flip overrides are preserved on later rediscoveries.
		if (input is { MatrixWidth: > 0, MatrixHeight: > 0 }) {
			var firstDetect = MatrixWidth == 0 || MatrixHeight == 0;
			MatrixWidth = input.MatrixWidth;
			MatrixHeight = input.MatrixHeight;
			if (firstDetect) {
				StripMode = StripMode.Matrix;
				MatrixMode = MatrixMode.Mirror;
			}
		}
	}

	/// <summary>
	///     UI Properties.
	/// </summary>
	public SettingsProperty[] KeyProperties {
		get => Kps();
		set { }
	}

	private SettingsProperty[] Kps() {
		var multiplier = new SettingsProperty("LedMultiplier", "ledMultiplier", "");
		switch (StripMode) {
			case StripMode.Matrix: {
				var matrixProps = new List<SettingsProperty> {
					new("StripMode", "select", "Strip Mode", new Dictionary<string, string> {
						["0"] = "Normal",
						["1"] = "Sectored",
						["2"] = "Loop (Play Bar)",
						["3"] = "Single Color",
						["4"] = "Matrix (2D)"
					}),
					new("MatrixWidth", "number", "Matrix Width") { ValueMin = "1" },
					new("MatrixHeight", "number", "Matrix Height") { ValueMin = "1" },
					new("MatrixMode", "select", "Matrix Mode", new Dictionary<string, string> {
						["2"] = "Mirror (full screen)",
						["3"] = "Extend (edge reflection)"
					}) {
						ValueHint =
							"Mirror: the literal full screen. Extend: reflect the chosen screen edge outward " +
							"across the panel, fading with distance, so it looks like the screen continues."
					},
					new("MatrixSide", "select", "Screen Side", new Dictionary<string, string> {
						["0"] = "Left",
						["1"] = "Right",
						["2"] = "Top",
						["3"] = "Bottom"
					}) {
						ValueHint = "Which screen edge the panel sits next to (the edge reflected in Extend mode)."
					}
				};

				if (MatrixMode == MatrixMode.Extend) {
					matrixProps.Add(new SettingsProperty("ExtendDepth", "number", "Reflection Depth %") {
						ValueMin = "1",
						ValueMax = "100",
						ValueHint = "How far into the screen (percent) the edge reflection samples."
					});
					matrixProps.Add(new SettingsProperty("ExtendFade", "number", "Edge Fade %") {
						ValueMin = "0",
						ValueMax = "100",
						ValueHint = "Brightness drop at the panel's far edge so the reflection fades into the distance."
					});
				}

				matrixProps.Add(new SettingsProperty("FlipHorizontal", "check", "Flip Horizontal"));
				matrixProps.Add(new SettingsProperty("FlipVertical", "check", "Flip Vertical"));

				// Matrix targets always stream over DDP (handles chunking + sync), so the WLED
				// realtime protocol and physical wiring are not exposed here - WLED owns both.
				return matrixProps.ToArray();
			}
			case StripMode.Single:
				return new[] {
					new("sectormap", "sectormap", ""),
					new("StripMode", "select", "Strip Mode", new Dictionary<string, string> {
						["0"] = "Normal",
						["1"] = "Sectored",
						["2"] = "Loop (Play Bar)",
						["3"] = "Single Color",
						["4"] = "Matrix (2D)"
					}),
					multiplier,
					new("ReverseStrip", "check", "Reverse Strip Direction"),
					new("Protocol", "select", "Streaming Protocol", new Dictionary<string, string> {
						["1"] = "WARLS",
						["2"] = "DRGB",
						["3"] = "DRGBW",
						["4"] = "DNRGB"
					}) { ValueHint = "Select desired WLED protocol. Default is DRGB." }
				};
			case StripMode.Sectored: {
				var props = new List<SettingsProperty> {
					new("sectorLedMap", "sectorLedMap", ""),
					new("StripMode", "select", "Strip Mode", new Dictionary<string, string> {
						["0"] = "Normal",
						["1"] = "Sectored",
						["2"] = "Loop (Play Bar)",
						["3"] = "Single Color",
						["4"] = "Matrix (2D)"
					}),
					new("Protocol", "select", "Streaming Protocol", new Dictionary<string, string> {
						["1"] = "WARLS",
						["2"] = "DRGB",
						["3"] = "DRGBW",
						["4"] = "DNRGB"
					}) { ValueHint = "Select desired WLED protocol. Default is DRGB." }
				};
				foreach(var seg in Segments) {
					props.Add(new SettingsProperty($"segmentOffset{seg.Id}", "number", $"Segment {seg.Id} Offset"));
					props.Add(new SettingsProperty($"segmentMultiplier{seg.Id}", "number", $"Segment {seg.Id} Multiplier"));
				}
				
				return props.ToArray();
			}
			case StripMode.Normal:
			case StripMode.Loop:
			default:
				return new[] {
					new("ledmap", "ledmap", ""),
					new("StripMode", "select", "Strip Mode", new Dictionary<string, string> {
						["0"] = "Normal",
						["1"] = "Sectored",
						["2"] = "Loop (Play Bar)",
						["3"] = "Single Color",
						["4"] = "Matrix (2D)"
					}),
					new("Offset", "number", "Strip Offset"),
					multiplier,
					new("ReverseStrip", "check", "Reverse Strip")
						{ ValueHint = "Reverse the order of the leds to clockwise (facing screen)." },
					new("Protocol", "select", "Streaming Protocol", new Dictionary<string, string> {
						["1"] = "WARLS",
						["2"] = "DRGB",
						["3"] = "DRGBW",
						["4"] = "DNRGB"
					}) { ValueHint = "Select desired WLED protocol. Default is DRGB." }
				};
		}
	}
}

/// <summary>
/// </summary>
public struct Ccnf {
	[JsonProperty("max")] public int Max { get; set; }
	[JsonProperty("min")] public int Min { get; set; }
	[JsonProperty("time")] public int Time { get; set; }
}

/// <summary>
///     Nightlight configuration
/// </summary>
public class Nl {
	/// <summary>
	///     Dim over course of nightlight duration.
	/// </summary>
	[JsonProperty("fade")]
	public bool Fade { get; set; }

	/// <summary>
	///     Enable nightlight mode.
	/// </summary>

	[JsonProperty("on")]
	public bool On { get; set; }

	/// <summary>
	///     NightLight duration.
	/// </summary>

	[JsonProperty("dur")]
	public int Duration { get; set; }

	/// <summary>
	///     Nightlight mode (0: instant, 1: fade, 2: color fade, 3: sunrise).
	/// </summary>
	[JsonProperty("mode")]
	public int Mode { get; set; }

	/// <summary>
	///     Target brightness of nightlight feature.
	/// </summary>
	[JsonProperty("tbri")]
	public int Tbri { get; set; }
}

public class Udpn {
	/// <summary>
	///     Receive broadcast packets
	/// </summary>
	[JsonProperty("recv")]
	public bool Recv { get; set; }

	/// <summary>
	///     Send WLED broadcast (UDP sync) packet on state change
	/// </summary>

	[JsonProperty("send")]
	public bool Send { get; set; }
}

public class WledSegment {
	/// <summary>
	///     Mirror the segment.
	/// </summary>
	[JsonProperty("mi")]
	public bool Mi { get; set; }

	/// <summary>
	///     Segment is enabled.
	/// </summary>

	[JsonProperty("on")]
	public bool On { get; set; }

	/// <summary>
	///     Flip the segment (reverse animation)
	/// </summary>
	[JsonProperty("rev")]
	public bool ReverseStrip { get; set; }

	/// <summary>
	///     True if segment is selected.
	/// </summary>
	[JsonProperty("sel")]
	public bool Sel { get; set; }

	/// <summary>
	///     Scale factor for LED counts related to master grid.
	/// </summary>
	[JsonProperty("LedMultiplier")]
	public float Multiplier { get; set; }

	/// <summary>
	///     Segment brightness.
	/// </summary>
	[JsonProperty("bri")]
	public int Brightness { get; set; }

	/// <summary>
	///     ID of segment effect.
	/// </summary>
	[JsonProperty("fx")]
	public int Fx { get; set; }

	/// <summary>
	///     Segment group
	/// </summary>
	[JsonProperty("grp")]
	public int Grp { get; set; }

	/// <summary>
	///     Segment ID.
	/// </summary>
	[JsonProperty("id")]
	public int Id { get; set; }

	/// <summary>
	///     Effect intensity.
	/// </summary>
	[JsonProperty("ix")]
	public int Ix { get; set; }

	/// <summary>
	///     Segment length.
	/// </summary>
	[JsonProperty("len")]
	public int LedCount { get; set; }

	/// <summary>
	///     Segment offset in reference to Glimmr master Grid.
	/// </summary>
	[JsonProperty("Offset")]
	public int Offset { get; set; }

	/// <summary>
	///     ID of the color palette.
	/// </summary>
	[JsonProperty("pal")]
	public int Pal { get; set; }


	/// <summary>
	///     Spacing?
	/// </summary>
	[JsonProperty("spc")]
	public int Spc { get; set; }

	/// <summary>
	///     Segment start position.
	/// </summary>
	[JsonProperty("start")]
	public int Start { get; set; }

	/// <summary>
	///     Segment end position.
	/// </summary>
	[JsonProperty("stop")]
	public int Stop { get; set; }

	/// <summary>
	///     Relative effect speed.
	/// </summary>
	[JsonProperty("sx")]
	public int Sx { get; set; }

	/// <summary>
	///     Segment colors
	/// </summary>
	[JsonProperty("col")]
	public List<List<int>>? Col { get; set; }
}

public struct WledState {
	/// <summary>
	///     Is the device on?
	/// </summary>
	[JsonProperty("on")]
	public bool On { get; set; }

	/// <summary>
	///     Missing?
	/// </summary>
	[JsonProperty("ccnf")]
	public Ccnf Ccnf { get; set; }

	/// <summary>
	///     WLED Brightness.
	/// </summary>
	[JsonProperty("bri")]
	public int Bri { get; set; }

	/// <summary>
	///     Live Data Override.
	/// </summary>
	[JsonProperty("lor")]
	public int Lor { get; set; }

	/// <summary>
	///     Main Segment.
	/// </summary>
	[JsonProperty("mainseg")]
	public int Mainseg { get; set; }

	/// <summary>
	///     ID of currently set playlist.
	/// </summary>
	[JsonProperty("pl")]
	public int Pl { get; set; }

	/// <summary>
	///     Id of currently set preset.
	/// </summary>
	[JsonProperty("ps")]
	public int Ps { get; set; }

	/// <summary>
	///     Bitwise indication of preset slots.
	/// </summary>
	[JsonProperty("pss")]
	public int Pss { get; set; }

	/// <summary>
	///     Duration of cross fade between different colors.
	/// </summary>
	[JsonProperty("transition")]
	public int Transition { get; set; }

	/// <summary>
	///     Individual segments.
	/// </summary>
	[JsonProperty("seg")]
	public WledSegment[] Segments { get; set; }

	/// <summary>
	///     Night light settings.
	/// </summary>
	[JsonProperty("nl")]
	public Nl Nl { get; set; }

	/// <summary>
	///     UDP settings.
	/// </summary>
	[JsonProperty("udpn")]
	public Udpn Udpn { get; set; }
}

public struct Leds {
	/// <summary>
	///     Is the strip RGB+W?
	/// </summary>
	[JsonProperty("rgbw")]
	public bool Rgbw { get; set; }

	/// <summary>
	///     Is the segment locked.
	/// </summary>
	[JsonProperty("seglock")]
	public bool Seglock { get; set; }

	/// <summary>
	///     Show white channel slider.
	/// </summary>
	[JsonProperty("wv")]
	public bool Wv { get; set; }

	/// <summary>
	///     Number of LEDs.
	/// </summary>
	[JsonProperty("count")]
	public int Count { get; set; }

	/// <summary>
	///     2D matrix dimensions, present only when a 2D matrix is configured.
	/// </summary>
	[JsonProperty("matrix")]
	public WledMatrixInfo? Matrix { get; set; }

	/// <summary>
	///     Maximum power.
	/// </summary>
	[JsonProperty("maxpwr")]
	public int Maxpwr { get; set; }

	/// <summary>
	///     Maximum number of segments.
	/// </summary>
	[JsonProperty("maxseg")]
	public int Maxseg { get; set; }

	/// <summary>
	///     Current LED power usage.
	/// </summary>
	[JsonProperty("pwr")]
	public int Pwr { get; set; }

	/// <summary>
	///     LED data pin.
	/// </summary>
	[JsonProperty("pin")]
	public List<int> Pin { get; set; }
}

public struct Wifi {
	/// <summary>
	///     Wifi channel.
	/// </summary>
	[JsonProperty("channel")]
	public int Channel { get; set; }

	/// <summary>
	///     Wifi RSSI.
	/// </summary>
	[JsonProperty("rssi")]
	public int Rssi { get; set; }

	/// <summary>
	///     Wifi signal strength.
	/// </summary>
	[JsonProperty("signal")]
	public int Signal { get; set; }

	/// <summary>
	///     Wifi bssid.
	/// </summary>
	[JsonProperty("bssid")]
	public string Bssid { get; set; }
}

public struct Info {
	/// <summary>
	///     Is live streaming enabled?
	/// </summary>
	[JsonProperty("live")]
	public bool Live { get; set; }

	/// <summary>
	///     UI Sync button options.
	/// </summary>
	[JsonProperty("str")]
	public bool Str { get; set; }

	/// <summary>
	///     Free memory heap.
	/// </summary>
	[JsonProperty("freeheap")]
	public int Freeheap { get; set; }

	/// <summary>
	///     Number of included effects.
	/// </summary>
	[JsonProperty("fxcount")]
	public int Fxcount { get; set; }
	
	/// <summary>
	///     IP address?
	/// </summary>
	[JsonProperty("lwip")]
	public int Lwip { get; set; }

	/// <summary>
	///     Debugging only.
	/// </summary>
	[JsonProperty("opt")]
	public int Opt { get; set; }

	/// <summary>
	///     Palette count.
	/// </summary>
	[JsonProperty("palcount")]
	public int Palcount { get; set; }

	/// <summary>
	///     Udp port.
	/// </summary>
	[JsonProperty("udpport")]
	public int Udpport { get; set; }

	/// <summary>
	///     System Uptime.
	/// </summary>
	[JsonProperty("uptime")]
	public int Uptime { get; set; }

	/// <summary>
	///     Build ID.
	/// </summary>
	[JsonProperty("vid")]
	public int Vid { get; set; }

	/// <summary>
	///     Number of current websocket clients.
	/// </summary>
	[JsonProperty("ws")]
	public int Ws { get; set; }

	/// <summary>
	///     LED Data.
	/// </summary>
	[JsonProperty("leds")]
	public Leds Leds { get; set; }

	/// <summary>
	///     Architecture.
	/// </summary>
	[JsonProperty("arch")]
	public string Arch { get; set; }

	/// <summary>
	///     Device brand.
	/// </summary>
	[JsonProperty("brand")]
	public string Brand { get; set; }

	/// <summary>
	///     Arduino core version.
	/// </summary>
	[JsonProperty("core")]
	public string Core { get; set; }

	/// <summary>
	///     LED IP.
	/// </summary>
	[JsonProperty("lip")]
	public string Lip { get; set; }

	/// <summary>
	///     Realtime datasource info.
	/// </summary>
	[JsonProperty("lm")]
	public string Lm { get; set; }

	/// <summary>
	///     Device MAC address.
	/// </summary>
	[JsonProperty("mac")]
	public string Mac { get; set; }

	/// <summary>
	///     Device name.
	/// </summary>
	[JsonProperty("name")]
	public string Name { get; set; }

	/// <summary>
	///     Product type.
	/// </summary>
	[JsonProperty("product")]
	public string Product { get; set; }

	/// <summary>
	///     Product version.
	/// </summary>
	[JsonProperty("ver")]
	public string Ver { get; set; }

	/// <summary>
	///     Wifi info.
	/// </summary>
	[JsonProperty("wifi")]
	public Wifi Wifi { get; set; }
}

public struct WledStateData {
	/// <summary>
	///     State info.
	/// </summary>
	[JsonProperty("info")]
	public Info Info { get; set; }

	/// <summary>
	///     WLED State.
	/// </summary>
	[JsonProperty("state")]
	public WledState WledState { get; set; }

}

public class IgnorePropertiesResolver : DefaultContractResolver {
	private readonly HashSet<string> ignoreProps;

	public IgnorePropertiesResolver(IEnumerable<string> propNamesToIgnore) {
		ignoreProps = new HashSet<string>(propNamesToIgnore, StringComparer.OrdinalIgnoreCase);
	}

	protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
		var property = base.CreateProperty(member, memberSerialization);

		if (property.PropertyName != null && ignoreProps.Contains(property.PropertyName)) {
			property.ShouldSerialize = _ => false;
		}

		return property;
	}
}

/// <summary>
///     2D matrix dimensions reported by WLED in <c>info.leds.matrix</c>.
/// </summary>
public class WledMatrixInfo {
	[JsonProperty("w")] public int W { get; set; }
	[JsonProperty("h")] public int H { get; set; }
}

/// <summary>
///     Minimal subset of WLED's <c>/json/cfg</c> needed to read 2D panel wiring.
/// </summary>
public class WledCfg {
	[JsonProperty("hw")] public WledCfgHw? Hw { get; set; }
}

public class WledCfgHw {
	[JsonProperty("led")] public WledCfgLed? Led { get; set; }
}

public class WledCfgLed {
	[JsonProperty("matrix")] public WledCfgMatrix? Matrix { get; set; }
}

public class WledCfgMatrix {
	[JsonProperty("mpc")] public int Mpc { get; set; }
	[JsonProperty("panels")] public List<WledPanel>? Panels { get; set; }
}

/// <summary>
///     A single WLED 2D panel. Field names mirror WLED's cfg.json.
/// </summary>
public class WledPanel {
	/// <summary>First LED at the bottom of the panel.</summary>
	[JsonProperty("b")] public bool Bottom { get; set; }

	/// <summary>First LED at the right of the panel.</summary>
	[JsonProperty("r")] public bool Right { get; set; }

	/// <summary>Panel is wired in columns (vertical) rather than rows.</summary>
	[JsonProperty("v")] public bool Vertical { get; set; }

	/// <summary>Panel wiring zig-zags (serpentine).</summary>
	[JsonProperty("s")] public bool Serpentine { get; set; }

	[JsonProperty("w")] public int W { get; set; }
	[JsonProperty("h")] public int H { get; set; }
}