using System;
using System.ComponentModel;
using Newtonsoft.Json;

namespace Glimmr.Models.LED {
    [Serializable]
    public class LedData {
        [JsonProperty] public int LedCount => LeftCount + RightCount + TopCount + BottomCount;

        [DefaultValue(18)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int GpioNumber { get; set; } = 18;

        [DefaultValue(55)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int MilliampsPerLed { get; set; } = 55;


        [DefaultValue(800)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int AblMaxMilliamps { get; set; } = 800;
        
        [JsonProperty] public bool AutoBrightnessLevel { get; set; }
        [JsonProperty] public int StripType { get; set; }
        [JsonProperty] public int Brightness { get; set; } = 255;
        [JsonProperty] public int StartupAnimation { get; set; }
        [JsonProperty] public int LeftCount { get; set;} = 24;
        [JsonProperty] public int RightCount { get; set; } = 24;
        [JsonProperty] public int TopCount { get; set; } = 40;
        [JsonProperty] public int BottomCount { get; set; } = 40;
        [JsonProperty] public int VCountDs { get; set; } = 24;
        [JsonProperty] public int HCountDs { get; set; } = 40;
        [JsonProperty] public bool FixGamma { get; set; } = true;

        
    }
}