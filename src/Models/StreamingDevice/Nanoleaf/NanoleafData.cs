using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;

namespace Glimmr.Models.StreamingDevice.Nanoleaf {
    [Serializable]
    public class NanoleafData : StreamingData {
        [JsonProperty] public string IpV6Address { get; set; }
        [JsonProperty] public string Hostname { get; set; }
        [JsonProperty] public int Port { get; set; }
        [JsonProperty] public string GroupName { get; set; }
        [JsonProperty] public string Token { get; set; }
        [JsonProperty] public int GroupNumber { get; set; }
        [JsonProperty] public string Type { get; set; }
        [JsonProperty] public string Version { get; set; }
        [JsonProperty] public int Mode { get; set; }
        [JsonProperty] public NanoLayout Layout { get; set; }

        [DefaultValue(0)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public float X { get; set; } = 0;

        [DefaultValue(50)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public float Y { get; set; } = 50;

        [DefaultValue(1)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public float Scale { get; set; } = 1;

        [DefaultValue(0)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public float Rotation { get; set; }

        [DefaultValue(false)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool MirrorX { get; set; }

        [DefaultValue(false)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool MirrorY { get; set; }

        public NanoleafData() {
            Tag = "Nanoleaf";
            Name ??= Tag;
            if (IpAddress != null) {
                var hc = string.GetHashCode(IpAddress, StringComparison.InvariantCulture);
                Name = "Nanoleaf - " + (string) hc.ToString(CultureInfo.InvariantCulture).Substring(0, 4);
            }
        }

        // Copy data from an existing leaf into this leaf...don't insert
        public static NanoleafData CopyExisting(NanoleafData newLeaf, NanoleafData existingLeaf) {
            if (existingLeaf == null || newLeaf == null) throw new ArgumentException("Invalid nano data!");
            newLeaf.Token = existingLeaf.Token;
            newLeaf.X = existingLeaf.X;
            newLeaf.Y = existingLeaf.Y;
            newLeaf.Scale = 1;
            newLeaf.Rotation = existingLeaf.Rotation;
            newLeaf.Enable = existingLeaf.Enable;
            if (existingLeaf.Brightness != 0)  newLeaf.Brightness = existingLeaf.Brightness;
            // Grab the new leaf layout
            newLeaf.RefreshLeaf();
            // Merge this data's layout with the existing leaf (copy sector)
            var newL = MergeLayouts(newLeaf.Layout, existingLeaf.Layout);
            newLeaf.Layout = newL;
            newLeaf.Tag = "Nanoleaf";
            newLeaf.Name ??= newLeaf.Tag;
            
            return newLeaf;
        }

        private static NanoLayout MergeLayouts(NanoLayout newLayout, NanoLayout existing) {
            if (newLayout == null) throw new ArgumentException("Invalid argument.");
            if (existing == null) return newLayout;
            var posData = new List<PanelLayout>();
            var output = newLayout;
            
            if (existing.PositionData == null) {
                return output;
            }

            // Loop through each panel in the new position data, find existing info and copy
            foreach (var nl in newLayout.PositionData) {
                foreach (var el in existing.PositionData.Where(s => s.PanelId == nl.PanelId)) {
                    nl.TargetSector = el.TargetSector;
                }
                posData.Add(nl);
            }
            output.PositionData = posData;
            return output;
        }

        public void RefreshLeaf() {
            if (Token == null) return;
            using var nl = new NanoleafDevice(IpAddress, Token);
            var layout = nl.GetLayout().Result;
            if (layout != null) Layout = layout;
            Scale = 1;
        }

        
    }
    [Serializable]
    public class NanoLayout {
        [JsonProperty] public int NumPanels { get; set; }
        [JsonProperty] public int SideLength { get; set; } = 1;
        [JsonProperty] public List<PanelLayout> PositionData { get; set; } = new List<PanelLayout>();

    }

    [Serializable]
    public class PanelLayout {
        [JsonProperty] public int PanelId { get; set; }
        [JsonProperty] public int X { get; set; }

        [DefaultValue(10)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int Y { get; set; } = 10;

        [JsonProperty] public int O { get; set; }

        [DefaultValue(-1)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int TargetSector { get; set; } = -1;

        [JsonProperty] public int ShapeType { get; set; }
    }
}