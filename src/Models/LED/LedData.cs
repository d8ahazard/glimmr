using System;
using System.ComponentModel;
using Newtonsoft.Json;

namespace Glimmr.Models.LED {
    [Serializable]
    public class LedData {

        [DefaultValue(18)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int GpioNumber { get; set; } = 18;

        [DefaultValue(55)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int MilliampsPerLed { get; set; } = 55;


        [DefaultValue(800)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int AblMaxMilliamps { get; set; } = 3000;
        
        [JsonProperty] public bool AutoBrightnessLevel { get; set; }
        [JsonProperty] public int StripType { get; set; }
        [JsonProperty] public int Brightness { get; set; } = 255;
        [JsonProperty] public int StartupAnimation { get; set; }
        [JsonProperty] public int Offset { get; set; }
        [JsonProperty] public int Count { get; set; }
        [JsonProperty] public bool FixGamma { get; set; } = true;
        [JsonProperty] public string Id { get; set; }
        
    }
}