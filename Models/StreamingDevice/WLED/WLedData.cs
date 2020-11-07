using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using Accord.Math;
using HueDream.Models.LED;
using HueDream.Models.Util;
using Newtonsoft.Json;

namespace HueDream.Models.StreamingDevice.WLed {
    public class WLedData {
        [JsonProperty] public string Id { get; set; }
        [JsonProperty] public string IpAddress { get; set; }
        [JsonProperty] public int Brightness { get; set; }
        // 0 = normal
        // 1 = all to one sector
        // 2 = Sub sectors
        [JsonProperty] public int StripMode { get; set; }
        // If in normal mode, set an optional offset, strip direction, horizontal count, and vertical count.
        [JsonProperty] public int Offset { get; set; }
        [JsonProperty] public int StripDirection { get; set; }
        [JsonProperty] public int HCount { get; set; }
        [JsonProperty] public int VCount { get; set; }
        // Applicable for both modes
        [JsonProperty] public int LedCount { get; set; }
        [JsonProperty] public List<int> Sectors { get; set; }
        [JsonProperty] public Dictionary<int, int> SubSectors { get; set; }
        
        [JsonProperty] public static string Tag = "WLed";
        [JsonProperty] public WLedStateData State { get; set; }
        [JsonProperty] public bool ControlStrip { get; set; }
        [JsonProperty] public bool AutoDisable { get; set; }

        public WLedData() {}
        public WLedData(string id) {
            Id = id;
            ControlStrip = false;
            AutoDisable = true;
            Sectors = new List<int>();
            SubSectors = new Dictionary<int, int>();
            LedData ld = new LedData();
            try {
                ld = DataUtil.GetItem<LedData>("ledData");
            } catch (KeyNotFoundException e) {
            }

            var capMode = DataUtil.GetItem<int>("captureMode");
            if (capMode == 0) {
                VCount = ld.VCountDs;
                HCount = ld.HCountDs;
            } else {
                VCount = ld.VCount;
                HCount = ld.HCount;
            }
            
            try {
                var dns = Dns.GetHostEntry(Id + ".local");
                if (dns.AddressList.Length > 0) {
                    IpAddress = dns.AddressList[0].ToString();
                }
            } catch (Exception e) {
                LogUtil.Write("DNS Res ex: " + e.Message);
            }

            using var webClient = new WebClient();
            try {
                var url = "http://" + Id + ".local/json";
                var jsonData = webClient.DownloadString(url);
                var jsonObj = JsonConvert.DeserializeObject<WLedStateData>(jsonData);
                State = jsonObj;
                LedCount = jsonObj.info.leds.count;
                Brightness = jsonObj.state.bri;
            } catch (Exception) {
                
            }
        }

        public void CopyExisting(WLedData input) {
            if (input == null) throw new ArgumentNullException(nameof(input));
            Offset = input.Offset;
            HCount = input.HCount;
            VCount = input.VCount;
            AutoDisable = input.AutoDisable;
            ControlStrip = input.ControlStrip;
            LedCount = input.LedCount;
            Brightness = input.Brightness;
            StripDirection = input.StripDirection;
            StripMode = input.StripMode;
        }
    }
}