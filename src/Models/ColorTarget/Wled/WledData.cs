#region

using System;
using System.Collections.Generic;
using System.ComponentModel;
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

		[JsonProperty] public int LedCount { get; set; }

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
		[JsonProperty] public WledStateData State { get; set; }

		private SettingsProperty[] _keyProperties;


		public WledData() {
			Tag = "Wled";
			Name ??= Tag;
			if (Id != null) {
				Name = StringUtil.UppercaseFirst(Id);
			}
		}

		public WledData(string id, string ipaddress) {
			Id = id;
			Tag = "Wled";
			Name ??= Tag;
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
				if (jsonObj == null) {
					return;
				}

				LedCount = jsonObj.info.leds.count;
				Brightness = (int) (jsonObj.state.bri / 255f * 100);
			} catch (Exception e) {
				Log.Debug("Yeah, here's your problem, smart guy: " + e.Message);
			}
		}

		public string LastSeen { get; set; }

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
			set => _keyProperties = value;
		}

		[JsonProperty] public string Name { get; set; }

		public string Id { get; set; }
		public string Tag { get; set; }
		public string IpAddress { get; set; }
		public int Brightness { get; set; }


		public bool Enable { get; set; }

		private SettingsProperty[] Kps() {
			if ((StripMode) StripMode == Enums.StripMode.Single) {
				return new SettingsProperty[] {
					new("sectormap", "sectormap", ""),
					new("LedCount", "number", "Led Count"),
					new("StripMode", "select", "Strip Mode", new Dictionary<string, string> {
						["0"] = "Normal",
						["1"] = "Sectored",
						["2"] = "Loop (Play Bar)",
						["3"] = "Single Color"
					}),
					new("ReverseStrip", "check", "Reverse Strip Direction")
				};
			}

			return new SettingsProperty[] {
				new("ledmap", "ledmap", ""),
				new("Offset", "number", "Strip Offset"),
				new("LedCount", "number", "Led Count"),
				new("StripMode", "select", "Strip Mode", new Dictionary<string, string> {
					["0"] = "Normal",
					["1"] = "Sectored",
					["2"] = "Loop (Play Bar)",
					["3"] = "Single Color"
				}),
				new("ReverseStrip", "check", "Reverse Strip Direction")
			};
		}
	}

	public class Ccnf {
		public int max { get; set; }
		public int min { get; set; }
		public int time { get; set; }
	}

	public class Nl {
		public bool fade { get; set; }
		public bool on { get; set; }
		public int dur { get; set; }
		public int mode { get; set; }
		public int tbri { get; set; }
	}

	public class Udpn {
		public bool recv { get; set; }
		public bool send { get; set; }
	}

	public class Seg {
		public bool mi { get; set; }
		public bool on { get; set; }
		public bool rev { get; set; }
		public bool sel { get; set; }
		public int bri { get; set; }
		public int fx { get; set; }
		public int grp { get; set; }
		public int id { get; set; }
		public int ix { get; set; }
		public int len { get; set; }
		public int pal { get; set; }
		public int spc { get; set; }
		public int start { get; set; }
		public int stop { get; set; }
		public int sx { get; set; }
		public List<List<int>> col { get; set; }
	}

	public class State {
		public bool on { get; set; }
		public Ccnf ccnf { get; set; }
		public int bri { get; set; }
		public int lor { get; set; }
		public int mainseg { get; set; }
		public int pl { get; set; }
		public int ps { get; set; }
		public int pss { get; set; }
		public int transition { get; set; }
		public List<Seg> seg { get; set; }
		public Nl nl { get; set; }
		public Udpn udpn { get; set; }
	}

	public class Leds {
		public bool rgbw { get; set; }
		public bool seglock { get; set; }
		public bool wv { get; set; }
		public int count { get; set; }
		public int maxpwr { get; set; }
		public int maxseg { get; set; }
		public int pwr { get; set; }
		public List<int> pin { get; set; }
	}

	public class Wifi {
		public int channel { get; set; }
		public int rssi { get; set; }
		public int signal { get; set; }
		public string bssid { get; set; }
	}

	public class Info {
		public bool live { get; set; }
		public bool str { get; set; }
		public int freeheap { get; set; }
		public int fxcount { get; set; }
		public int lwip { get; set; }
		public int opt { get; set; }
		public int palcount { get; set; }
		public int udpport { get; set; }
		public int uptime { get; set; }
		public int vid { get; set; }
		public int ws { get; set; }
		public Leds leds { get; set; }
		public string arch { get; set; }
		public string brand { get; set; }
		public string core { get; set; }
		public string lip { get; set; }
		public string lm { get; set; }
		public string mac { get; set; }
		public string name { get; set; }
		public string product { get; set; }
		public string ver { get; set; }
		public Wifi wifi { get; set; }
	}

	public class WledStateData {
		public Info info { get; set; }
		public State state { get; set; }
	}
}