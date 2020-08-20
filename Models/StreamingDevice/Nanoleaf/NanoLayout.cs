using System;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

namespace HueDream.Models.StreamingDevice.Nanoleaf {
    // This one stores all the panel info
    [Serializable]
    public class NanoLayout {
        [JsonProperty] public int NumPanels { get; set; }
        [JsonProperty] public int SideLength { get; set; }
        [JsonProperty] public List<PanelLayout> PositionData { get; set; }

        public NanoLayout() {
            PositionData = new List<PanelLayout>();
        }
    }

    [Serializable]
    public class PanelLayout {
        [JsonProperty] public int PanelId { get; set; }
        [JsonProperty] public int X { get; set; }

        [DefaultValue(10)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int Y { get; set; }

        [JsonProperty] public int O { get; set; }

        [DefaultValue(-1)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int TargetSector { get; set; }
        
        [DefaultValue(-1)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int TargetSectorV2 { get; set; }

        [JsonProperty] public int ShapeType { get; set; }
    }
}