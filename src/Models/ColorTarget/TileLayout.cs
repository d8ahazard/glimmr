using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using LifxNetPlus;
using Newtonsoft.Json;

namespace Glimmr.Models.ColorTarget {
	[Serializable]
        public class TileLayout {
            public int NumPanels { get; set; }
            public int SideLength { get; set; }
            public TileData[] PositionData { get; set; }
            public TileLayout() {
            }
            public TileLayout(global::Nanoleaf.Client.Models.Responses.Layout layout) {
                NumPanels = layout.NumPanels;
                SideLength = layout.SideLength;
                PositionData = layout.PositionData.Select(l => new TileData(l)).ToArray();
            }
            
            public TileLayout(StateDeviceChainResponse layout) {
                NumPanels = layout.TotalCount;
                var pos = new List<TileData>();
                var idx = 0;
                foreach (var pd in layout.Tiles) {
                    pos.Add(new TileData(pd, idx));
                    idx++;
                }
                PositionData = pos.ToArray();
            }
    
            public void MergeLayout(TileLayout newLayout) {
                if (newLayout == null) throw new ArgumentException("Invalid argument.");
                if (PositionData == null) {
                    PositionData = newLayout.PositionData;
                    return;
                }

                var posData = new TileData[newLayout.PositionData.Length];
                // Loop through each panel in the new position data, find existing info and copy
                for (var i = 0; i < newLayout.PositionData.Length; i++) {
                    var nl = newLayout.PositionData[i];
                    foreach (var el in PositionData.Where(s => s.PanelId == nl.PanelId)) {
                        nl.TargetSector = el.TargetSector;
                    }
                    posData[i] = nl;
                }

                NumPanels = newLayout.NumPanels;
                SideLength = newLayout.SideLength;
                PositionData = posData;
            }
        }
    
        [Serializable]
        public class TileData {
    
            [DefaultValue(-1)]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public int TargetSector { get; set; } = -1;
            public int PanelId { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public int O { get; set; }
            
            public int ShapeType { get; set; }
    
            public TileData() {}
            
            public TileData(Tile t, int index) {
                X = (int) t.UserX;
                Y = (int) t.UserY;
                PanelId = index;
                ShapeType = 2;
            }
            
            public TileData(global::Nanoleaf.Client.Models.Responses.PanelLayout layout) {
                PanelId = layout.PanelId;
                X = layout.X;
                Y = layout.Y;
                O = layout.O;
                ShapeType = layout.ShapeType;
            }
        }
}