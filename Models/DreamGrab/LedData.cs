using System;
using Newtonsoft.Json;

namespace HueDream.Models.DreamGrab {
    [Serializable]
    public class LedData {
        [JsonProperty] public int LedCount { get; set; }
        [JsonProperty] public int PinNumber { get; set; }
        [JsonProperty] public int StripType { get; set; }
        [JsonProperty] public int Brightness { get; set; }
        [JsonProperty] public int StartupAnimation { get; set; }
        [JsonProperty] public int VCount { get; set; }
        [JsonProperty] public int HCount { get; set; }
        [JsonProperty] public int VCountDs { get; set; }
        [JsonProperty] public int HCountDs { get; set; }

        public LedData() {}

        public LedData(bool init) {
            if (!init) return;
            VCount = 16;
            HCount = 24;
            VCountDs = 16;
            HCountDs = 24;
            LedCount = VCount + VCount + HCount + HCount;
            PinNumber = 18;
            StripType = 2812;
            Brightness = 255;
            StartupAnimation = 0;
        }
    }
}