using System.ComponentModel;
using Newtonsoft.Json;

namespace Glimmr.Models.StreamingDevice {
    public abstract class StreamingData {

        [JsonProperty] public string Name { get; set; } = "";

        [JsonProperty] public string Id { get; set; } = "";
        
        [JsonProperty]
        public string Tag { get; set; }
        
        [JsonProperty] 
        public string IpAddress { get; set; }

        [DefaultValue(255)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int Brightness { get; set; } = 255;

        [DefaultValue(false)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool Enable { get; set; }

    }
}