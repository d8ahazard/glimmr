using System;
using System.ComponentModel;
using Newtonsoft.Json;

namespace Glimmr.Models.ColorTarget {
    public interface IColorTargetData {

        [DefaultValue("")]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public string Name { get; set; }

        [DefaultValue("")]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public string Id { get; set; }
        
        [JsonProperty]
        public string Tag { get; set; }
        
        [JsonProperty] 
        public string IpAddress { get; set; }

        [DefaultValue(255)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int Brightness { get; set; }

        [DefaultValue(false)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool Enable { get; set; }
        
        [DefaultValue("")]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public string LastSeen { get; set; }

        public void CopyExisting(IColorTargetData data);
    }
}