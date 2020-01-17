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
        [JsonProperty] public int CountLeft { get; set; }
        [JsonProperty] public int CountRight { get; set; }
        [JsonProperty] public int CountTop { get; set; }
        [JsonProperty] public int CountBottom { get; set; }
        [JsonProperty] public int CamType { get; set; }
        [JsonProperty] public int StreamId { get; set; }

        [JsonProperty] public bool UseLed { get; set; }

        public LedData() {}

        public LedData(bool init) {
            if (!init) return;
            CountLeft = 16;
            CountRight = 16;
            CountTop = 24;
            CountBottom = 24;
            LedCount = CountLeft + CountRight + CountTop + CountBottom;
            PinNumber = 18;
            StripType = 2812;
            Brightness = 255;
            StartupAnimation = 0;
            CamType = 1;
            StreamId = 0;
            UseLed = false;
        }
    }
}