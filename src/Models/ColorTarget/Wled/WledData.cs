using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using Glimmr.Models.ColorTarget.LED;
using Glimmr.Models.Util;
using Newtonsoft.Json;
using Serilog;

namespace Glimmr.Models.ColorTarget.Wled {
    public class WledData : StreamingData {
        // 0 = normal
        // 1 = all to one sector
        // 2 = Sub sectors
        [JsonProperty] public int StripMode { get; set; }
        // If in normal mode, set an optional offset, strip direction, horizontal count, and vertical count.
        [JsonProperty] public int Offset { get; set; }
        
        [DefaultValue(false)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool ReverseStrip { get; set; }
        [JsonProperty] public int LedCount { get; set; }
        [JsonProperty] public List<int> Sectors { get; set; }
        [JsonProperty] public Dictionary<int, int> SubSectors { get; set; }
        [JsonProperty] public WledStateData State { get; set; }
        [JsonProperty] public bool ControlStrip { get; set; }
        [JsonProperty] public bool AutoDisable { get; set; }
        
        
        public WledData() {
            Tag = "Wled";
            Name ??= Tag;
            if (Id != null) Name = StringUtil.UppercaseFirst(Id);
        }
        public WledData(string id) {
            Id = id;
            Tag = "Wled";
            Name ??= Tag;
            if (Id != null) Name = StringUtil.UppercaseFirst(Id);
            ControlStrip = false;
            AutoDisable = true;
            Sectors = new List<int>();
            SubSectors = new Dictionary<int, int>();
            LedData ld = new LedData();
            
            try {
                var dns = Dns.GetHostEntry(Id + ".local");
                if (dns.AddressList.Length > 0) {
                    IpAddress = dns.AddressList[0].ToString();
                }
            } catch (Exception e) {
                Log.Debug("DNS Res ex: " + e.Message);
            }

            using var webClient = new WebClient();
            try {
                var url = "http://" + Id + ".local/json";
                var jsonData = webClient.DownloadString(url);
                var jsonObj = JsonConvert.DeserializeObject<WledStateData>(jsonData);
                State = jsonObj;
                LedCount = jsonObj.info.leds.count;
                Brightness = jsonObj.state.bri;
            } catch (Exception) {
                
            }
        }

        public void CopyExisting(WledData input) {
            if (input == null) throw new ArgumentNullException(nameof(input));
            Offset = input.Offset;
            Enable = input.Enable;
            AutoDisable = input.AutoDisable;
            ControlStrip = input.ControlStrip;
            LedCount = input.LedCount;
            Brightness = input.Brightness;
            ReverseStrip = input.ReverseStrip;
            StripMode = input.StripMode;
            if (Id != null) Name = StringUtil.UppercaseFirst(Id);
        }
    }
    
    public class Ccnf    {
        public int min { get; set; } 
        public int max { get; set; } 
        public int time { get; set; } 
    }

    public class Nl    {
        public bool on { get; set; } 
        public int dur { get; set; } 
        public bool fade { get; set; } 
        public int mode { get; set; } 
        public int tbri { get; set; } 
    }

    public class Udpn    {
        public bool send { get; set; } 
        public bool recv { get; set; } 
    }

    public class Seg    {
        public int id { get; set; } 
        public int start { get; set; } 
        public int stop { get; set; } 
        public int len { get; set; } 
        public int grp { get; set; } 
        public int spc { get; set; } 
        public bool on { get; set; } 
        public int bri { get; set; } 
        public List<List<int>> col { get; set; } 
        public int fx { get; set; } 
        public int sx { get; set; } 
        public int ix { get; set; } 
        public int pal { get; set; } 
        public bool sel { get; set; } 
        public bool rev { get; set; } 
        public bool mi { get; set; } 
    }

    public class State    {
        public bool on { get; set; } 
        public int bri { get; set; } 
        public int transition { get; set; } 
        public int ps { get; set; } 
        public int pss { get; set; } 
        public int pl { get; set; } 
        public Ccnf ccnf { get; set; } 
        public Nl nl { get; set; } 
        public Udpn udpn { get; set; } 
        public int lor { get; set; } 
        public int mainseg { get; set; } 
        public List<Seg> seg { get; set; } 
    }

    public class Leds    {
        public int count { get; set; } 
        public bool rgbw { get; set; } 
        public bool wv { get; set; } 
        public List<int> pin { get; set; } 
        public int pwr { get; set; } 
        public int maxpwr { get; set; } 
        public int maxseg { get; set; } 
        public bool seglock { get; set; } 
    }

    public class Wifi    {
        public string bssid { get; set; } 
        public int rssi { get; set; } 
        public int signal { get; set; } 
        public int channel { get; set; } 
    }

    public class Info    {
        public string ver { get; set; } 
        public int vid { get; set; } 
        public Leds leds { get; set; } 
        public bool str { get; set; } 
        public string name { get; set; } 
        public int udpport { get; set; } 
        public bool live { get; set; } 
        public string lm { get; set; } 
        public string lip { get; set; } 
        public int ws { get; set; } 
        public int fxcount { get; set; } 
        public int palcount { get; set; } 
        public Wifi wifi { get; set; } 
        public string arch { get; set; } 
        public string core { get; set; } 
        public int lwip { get; set; } 
        public int freeheap { get; set; } 
        public int uptime { get; set; } 
        public int opt { get; set; } 
        public string brand { get; set; } 
        public string product { get; set; } 
        public string mac { get; set; } 
    }

    public class WledStateData {
        public State state { get; set; }
        public Info info { get; set; }
    }
}