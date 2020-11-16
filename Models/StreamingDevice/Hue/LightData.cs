using System;
using Newtonsoft.Json;
using Q42.HueApi;

namespace Glimmr.Models.StreamingDevice.Hue {
    [Serializable]
    public class LightData {
        public LightData() {
            Name = string.Empty;
            Type = string.Empty;
            Id = string.Empty;
            ModelId = string.Empty;
            TargetSector = -1;
            TargetSectorV2 = -1;
            Brightness = 100;
            OverrideBrightness = false;
            LastState = null;
        }

        public LightData(Light l) {
            if (l == null) return;
            Name = l.Name;
            Id = l.Id;
            Type = l.Type;
            ModelId = l.ModelId;
            TargetSector = -1;
            TargetSectorV2 = -1;
            Brightness = 100;
            OverrideBrightness = false;
            LastState = l.State;
        }

        [JsonProperty] public string Name { get; set; }
        [JsonProperty] public string Id { get; set; }
        [JsonProperty] public string Type { get; set; }
        [JsonProperty] public int Brightness { get; set; }
        [JsonProperty] public bool OverrideBrightness { get; set; }
        [JsonProperty] public int TargetSector { get; set; }
        [JsonProperty] public int TargetSectorV2 { get; set; }
        [JsonProperty] public State LastState { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public string ModelId { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public int Presence { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public int LightLevel { get; set; }
    }
}