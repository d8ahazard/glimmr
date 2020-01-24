using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HueDream.Models.Nanoleaf {
    // This one stores all the panel info
    [Serializable]
    public class NanoLayout {
        [JsonProperty]
        public int NumPanels { get; set; }
        [JsonProperty]
        public int SideLength { get; set; }
        [JsonProperty]
        public List<PanelLayout> PositionData { get; set; }

    }
    [Serializable]
    public class PanelLayout {
        [JsonProperty]
        public int PanelId { get; set; }
        [JsonProperty]
        public int X { get; set; }
        [JsonProperty]
        public int Y { get; set; }
        [JsonProperty]
        public int O { get; set; }
        public int ShapeType { get; set; }
    }
}
