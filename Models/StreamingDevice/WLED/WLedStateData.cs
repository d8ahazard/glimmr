using System.Collections.Generic;

namespace Glimmr.Models.StreamingDevice.WLed {
     
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

    public class WLedStateData {
        public State state { get; set; }
        public Info info { get; set; }
    }

}