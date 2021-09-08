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
	public class WledData : IColorTargetData {
		[JsonProperty] public bool AutoDisable { get; set; }
		[JsonProperty] public bool ControlStrip { get; set; }


		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool ReverseStrip { get; set; }

		[JsonProperty] public Dictionary<int, int> SubSectors { get; set; }
		[JsonProperty] public int Brightness { get; set; }

		[JsonProperty] public int LedCount { get; set; }
		[JsonProperty] public int LedMultiplier { get; set; } = 1;

		// If in normal mode, set an optional offset, strip direction, horizontal count, and vertical count.
		[JsonProperty] public int Offset { get; set; }

		// 0 = normal
		// 1 = all to one sector
		// 2 = Sub sectors
		[JsonProperty] public int StripMode { get; set; }

		[DefaultValue(-1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int TargetSector { get; set; }

		[JsonProperty] public List<int> Sectors { get; set; }
		[JsonProperty] public string Tag { get; set; }
		[JsonProperty] public WledStateData State { get; set; }


		public WledData() {
			Tag = "Wled";
			Name ??= Tag;
			Id = "";
			IpAddress = "";
			ControlStrip = false;
			AutoDisable = true;
			Sectors = new List<int>();
			SubSectors = new Dictionary<int, int>();
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
			ControlStrip = false;
			AutoDisable = true;
			Sectors = new List<int>();
			SubSectors = new Dictionary<int, int>();
			using var webClient = new WebClient();
			try {
				var url = "http://" + IpAddress + "/json";
				var jsonData = webClient.DownloadString(url);
				var jsonObj = JsonConvert.DeserializeObject<WledStateData>(jsonData);
				State = jsonObj;

				LedCount = jsonObj.Info.Leds.Count;
				Brightness = (int) (jsonObj.State.Bri / 255f * 100);
			} catch (Exception e) {
				Log.Debug("Yeah, here's your problem, smart guy: " + e.Message);
			}
		}

		[JsonProperty] public string Name { get; set; }
		[JsonProperty] public string Id { get; set; }
		[JsonProperty] public string IpAddress { get; set; }
		[JsonProperty] public bool Enable { get; set; }
		[JsonProperty] public string LastSeen { get; set; }


		public void UpdateFromDiscovered(IColorTargetData data) {
			var input = (WledData) data;
			if (input == null) {
				throw new ArgumentNullException(nameof(input));
			}

			AutoDisable = input.AutoDisable;
			ControlStrip = input.ControlStrip;
			LedCount = input.LedCount;
			IpAddress = data.IpAddress;
			Name = StringUtil.UppercaseFirst(input.Name);
		}

		public SettingsProperty[] KeyProperties {
			get => Kps();
			set { }
		}

		private SettingsProperty[] Kps() {
			var multiplier = new SettingsProperty("LedMultiplier", "number", "LED Multiplier") {
				ValueMin = "-5", ValueStep = "1", ValueMax = "5",
				ValueHint = "Positive values to multiply (skip), negative values to divide (duplicate)."
			};
			if ((StripMode) StripMode == Enums.StripMode.Single) {
				return new[] {
					new("sectormap", "sectormap", ""),
					new("StripMode", "select", "Strip Mode", new Dictionary<string, string> {
						["0"] = "Normal",
						["1"] = "Sectored",
						["2"] = "Loop (Play Bar)",
						["3"] = "Single Color"
					}),
					new("LedCount", "number", "Led Count"),
					new("ReverseStrip", "check", "Reverse Strip Direction"),
					multiplier
				};
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
				new("ReverseStrip", "check", "Reverse Strip")
					{ValueHint = "Reverse the order of the leds to clockwise (facing screen)."},
				multiplier
			};
		}
	}

	public struct Ccnf {
		[JsonProperty("max")] public int Max { get; set; }
		[JsonProperty("min")] public int Min { get; set; }
		[JsonProperty("time")] public int Time { get; set; }
	}

	public class Nl {
		[JsonProperty("fade")] public bool Fade { get; set; }

		[JsonProperty("on")] public bool On { get; set; }

		[JsonProperty("dur")] public int Duration { get; set; }

		[JsonProperty("mode")] public int Mode { get; set; }

		[JsonProperty("tbri")] public int Tbri { get; set; }
	}

	public class Udpn {
		[JsonProperty("recv")] public bool Recv { get; set; }

		[JsonProperty("send")] public bool Send { get; set; }
	}

	public class Seg {
		[JsonProperty("mi")] public bool Mi { get; set; }

		[JsonProperty("on")] public bool On { get; set; }

		[JsonProperty("rev")] public bool Rev { get; set; }

		[JsonProperty("sel")] public bool Sel { get; set; }

		[JsonProperty("bri")] public int Bri { get; set; }

		[JsonProperty("fx")] public int Fx { get; set; }

		[JsonProperty("grp")] public int Grp { get; set; }

		[JsonProperty("id")] public int Id { get; set; }

		[JsonProperty("ix")] public int Ix { get; set; }

		[JsonProperty("len")] public int Len { get; set; }

		[JsonProperty("pal")] public int Pal { get; set; }

		[JsonProperty("spc")] public int Spc { get; set; }

		[JsonProperty("start")] public int Start { get; set; }

		[JsonProperty("stop")] public int Stop { get; set; }

		[JsonProperty("sx")] public int Sx { get; set; }

		[JsonProperty("col")] public List<List<int>>? Col { get; set; }
	}

	public struct State {
		[JsonProperty("on")] public bool On { get; set; }
		[JsonProperty("ccnf")] public Ccnf Ccnf { get; set; }
		[JsonProperty("bri")] public int Bri { get; set; }
		[JsonProperty("lor")] public int Lor { get; set; }
		[JsonProperty("mainseg")] public int Mainseg { get; set; }
		[JsonProperty("pl")] public int Pl { get; set; }
		[JsonProperty("ps")] public int Ps { get; set; }
		[JsonProperty("pss")] public int Pss { get; set; }
		[JsonProperty("transition")] public int Transition { get; set; }
		[JsonProperty("seg")] public List<Seg> Seg { get; set; }
		[JsonProperty("nl")] public Nl Nl { get; set; }
		[JsonProperty("udpn")] public Udpn Udpn { get; set; }
	}

	public struct Leds {
		[JsonProperty("rgbw")] public bool Rgbw { get; set; }
		[JsonProperty("seglock")] public bool Seglock { get; set; }
		[JsonProperty("wv")] public bool Wv { get; set; }
		[JsonProperty("count")] public int Count { get; set; }
		[JsonProperty("maxpwr")] public int Maxpwr { get; set; }
		[JsonProperty("maxseg")] public int Maxseg { get; set; }
		[JsonProperty("pwr")] public int Pwr { get; set; }
		[JsonProperty("pin")] public List<int> Pin { get; set; }
	}

	public struct Wifi {
		[JsonProperty("channel")] public int Channel { get; set; }
		[JsonProperty("rssi")] public int Rssi { get; set; }
		[JsonProperty("signal")] public int Signal { get; set; }
		[JsonProperty("bssid")] public string Bssid { get; set; }
	}

	public struct Info {
		[JsonProperty("live")] public bool Live { get; set; }
		[JsonProperty("str")] public bool Str { get; set; }
		[JsonProperty("freeheap")] public int Freeheap { get; set; }
		[JsonProperty("fxcount")] public int Fxcount { get; set; }
		[JsonProperty("lwip")] public int Lwip { get; set; }
		[JsonProperty("opt")] public int Opt { get; set; }
		[JsonProperty("palcount")] public int Palcount { get; set; }
		[JsonProperty("udpport")] public int Udpport { get; set; }
		[JsonProperty("uptime")] public int Uptime { get; set; }
		[JsonProperty("vid")] public int Vid { get; set; }
		[JsonProperty("ws")] public int Ws { get; set; }
		[JsonProperty("leds")] public Leds Leds { get; set; }
		[JsonProperty("arch")] public string Arch { get; set; }
		[JsonProperty("brand")] public string Brand { get; set; }
		[JsonProperty("core")] public string Core { get; set; }
		[JsonProperty("lip")] public string Lip { get; set; }
		[JsonProperty("lm")] public string Lm { get; set; }
		[JsonProperty("mac")] public string Mac { get; set; }
		[JsonProperty("name")] public string Name { get; set; }
		[JsonProperty("product")] public string Product { get; set; }
		[JsonProperty("ver")] public string Ver { get; set; }
		[JsonProperty("wifi")] public Wifi Wifi { get; set; }
	}

	public struct WledStateData {
		[JsonProperty("info")] public Info Info { get; set; }
		[JsonProperty("state")] public State State { get; set; }
	}
}