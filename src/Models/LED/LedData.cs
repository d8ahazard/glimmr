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


        [DefaultValue(2000)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int AblMaxMilliamps { get; set; } = 2000;
        
        [JsonProperty] public bool AutoBrightnessLevel { get; set; }
        [JsonProperty] public int StripType { get; set; }
        [JsonProperty] public int Brightness { get; set; } = 255;
        [JsonProperty] public int StartupAnimation { get; set; }
        
        [DefaultValue(0)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int Offset { get; set; }
        
        [DefaultValue(300)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int Count { get; set; } = 300;
        
        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool FixGamma { get; set; } = true;
        [JsonProperty] public string Id { get; set; }
        
        [DefaultValue(false)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool Enable { get; set; } = false;

    }
}