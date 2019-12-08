using System;
using Newtonsoft.Json;
using Q42.HueApi;

namespace HueDream.Hue {
    [Serializable]
    public class LightData {
        [JsonProperty] public string Name { get; set; }
        [JsonProperty] public string Id { get; set; }
        [JsonProperty] public string Type { get; set; }
        [JsonProperty] public string ModelId { get; set; }

        public LightData(Light l) {
            Name = l.Name;
            Id = l.Id;
            Type = l.Type;
            ModelId = l.ModelId;
        }
    }
}