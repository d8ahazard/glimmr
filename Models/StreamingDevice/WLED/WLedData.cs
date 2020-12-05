using System;
using System.Collections.Generic;
using System.Net;
using Glimmr.Models.LED;
using Glimmr.Models.Util;
using Newtonsoft.Json;

namespace Glimmr.Models.StreamingDevice.WLed {
    public class WLedData : StreamingData {
        // 0 = normal
        // 1 = all to one sector
        // 2 = Sub sectors
        [JsonProperty] public int StripMode { get; set; }
        // If in normal mode, set an optional offset, strip direction, horizontal count, and vertical count.
        [JsonProperty] public int Offset { get; set; }
        [JsonProperty] public int StripDirection { get; set; }
        [JsonProperty] public int TopCount { get; set; }
        [JsonProperty] public int LeftCount { get; set; }
        [JsonProperty] public int BottomCount { get; set; }
        [JsonProperty] public int RightCount { get; set; }
        // Applicable for both modes
        [JsonProperty] public int LedCount { get; set; }
        [JsonProperty] public List<int> Sectors { get; set; }
        [JsonProperty] public Dictionary<int, int> SubSectors { get; set; }
        [JsonProperty] public WLedStateData State { get; set; }
        [JsonProperty] public bool ControlStrip { get; set; }
        [JsonProperty] public bool AutoDisable { get; set; }

        public WLedData() {
            Tag = "Wled";
            Name ??= Tag;
            if (Id != null) Name = StringUtil.UppercaseFirst(Id);
        }
        public WLedData(string id) {
            Id = id;
            Tag = "Wled";
            Name ??= Tag;
            if (Id != null) Name = StringUtil.UppercaseFirst(Id);
            ControlStrip = false;
            AutoDisable = true;
            Sectors = new List<int>();
            SubSectors = new Dictionary<int, int>();
            LedData ld = new LedData(true);
            try {
                ld = DataUtil.GetObject<LedData>("LedData");
            } catch (KeyNotFoundException e) {
            }

            var capMode = DataUtil.GetItem<int>("captureMode");
            if (capMode == 0) {
                LeftCount = ld.VCountDs;
                TopCount = ld.HCountDs;
                RightCount = ld.VCountDs;
                BottomCount = ld.VCountDs;
            } else {
                LeftCount = ld.LeftCount;
                TopCount = ld.TopCount;
                RightCount = ld.RightCount;
                BottomCount = ld.BottomCount;
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
            TopCount = input.TopCount;
            LeftCount = input.LeftCount;
            RightCount = input.RightCount;
            BottomCount = input.BottomCount;
            // Probably don't need this, but...ehhh...
            if (RightCount == 0) RightCount = LeftCount;
            if (BottomCount == 0) BottomCount = TopCount;
            AutoDisable = input.AutoDisable;
            ControlStrip = input.ControlStrip;
            LedCount = input.LedCount;
            Brightness = input.Brightness;
            StripDirection = input.StripDirection;
            StripMode = input.StripMode;
            if (Id != null) Name = StringUtil.UppercaseFirst(Id);
        }
    }
}