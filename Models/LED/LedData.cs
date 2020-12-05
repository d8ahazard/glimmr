using System;
using Newtonsoft.Json;

namespace Glimmr.Models.LED {
    [Serializable]
    public class LedData {
        [JsonProperty] public int LedCount { get; set; }
        [JsonProperty] public int PinNumber { get; set; }
        [JsonProperty] public int StripType { get; set; }
        [JsonProperty] public int Brightness { get; set; }
        [JsonProperty] public int StartupAnimation { get; set; }
        [JsonProperty] public int LeftCount { get; set; }
        [JsonProperty] public int RightCount { get; set; }
        [JsonProperty] public int TopCount { get; set; }
        [JsonProperty] public int BottomCount { get; set; }
        [JsonProperty] public int VCountDs { get; set; }
        [JsonProperty] public int HCountDs { get; set; }
        [JsonProperty] public bool FixGamma { get; set; }

        public LedData() {
            LedCount = LeftCount + RightCount + TopCount + BottomCount;
        }

        public LedData(bool init) {
            if (!init) return;
            LeftCount = 16;
            TopCount = 24;
            RightCount = 16;
            BottomCount = 24;
            VCountDs = 16;
            HCountDs = 24;
            LedCount = LeftCount + RightCount + TopCount + BottomCount;
            PinNumber = 18;
            StripType = 2812;
            Brightness = 255;
            StartupAnimation = 0;
            FixGamma = true;
        }
    }
}