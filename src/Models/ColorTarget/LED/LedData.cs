using System;
using System.ComponentModel;
using Newtonsoft.Json;

namespace Glimmr.Models.ColorTarget.LED {
    [Serializable]
    public class LedData : StreamingData {

        [DefaultValue(18)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int GpioNumber { get; set; } = 18;

        [DefaultValue(55)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int MilliampsPerLed { get; set; } = 25;


        [DefaultValue(2000)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int AblMaxMilliamps { get; set; } = 5000;
        
        [JsonProperty] public bool AutoBrightnessLevel { get; set; }
        [JsonProperty] public int StripType { get; set; }
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
        
        [DefaultValue("Led")]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public new string Tag { get; set; } = "Led";
        
        
    }
}