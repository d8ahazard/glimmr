using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using HueDream.Models.Util;
using Newtonsoft.Json;

namespace HueDream.Models.StreamingDevice.Nanoleaf {
    [Serializable]
    public class NanoData {
        [JsonProperty] public const string Tag = "NanoLeaf";
        [JsonProperty] public string IpAddress { get; set; }
        [JsonProperty] public string IpV6Address { get; set; }
        [JsonProperty] public string Hostname { get; set; }
        [JsonProperty] public string Name { get; set; }
        [JsonProperty] public string Id { get; set; }
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
        public float X { get; set; }

        [DefaultValue(50)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public float Y { get; set; }

        [DefaultValue(1)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public float Scale { get; set; }

        [DefaultValue(0)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public float Rotation { get; set; }

        [DefaultValue(false)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool MirrorX { get; set; }

        [DefaultValue(false)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool MirrorY { get; set; }

        [DefaultValue(100)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int Brightness { get; set; }


        // Copy data from an existing leaf into this leaf...don't insert
        public void CopyExisting(NanoData leaf) {
            if (leaf == null) throw new ArgumentException("Invalid nano data!");
            Token = leaf.Token;
            X = leaf.X;
            Y = leaf.Y;
            Scale = 1;
            Rotation = leaf.Rotation;
            if (leaf.Brightness != 0)  Brightness = leaf.Brightness;
            // Grab the new leaf layout
            RefreshLeaf();
            // Merge this data's layout with the existing leaf (copy sector)
            var newL = MergeLayouts(leaf.Layout);
            Layout = newL;
        }

        private NanoLayout MergeLayouts(NanoLayout existing) {
            var posData = new List<PanelLayout>();
            var output = Layout;
            if (existing == null) {
                LogUtil.Write("Source is null, returning.");
                return output;
            }
            
            if (existing.PositionData == null) {
                return output;
            }

            foreach (var d in Layout.PositionData) {
                foreach (var s in existing.PositionData.Where(s => s.PanelId == d.PanelId)) {
                    LogUtil.Write("Copying existing panel sector mapping...");
                    d.Sector = s.Sector;
                }
                posData.Add(d);
            }
            output.PositionData = posData;
            return output;
        }

        public void RefreshLeaf() {
            if (Token == null) return;
            using var nl = new NanoGroup(IpAddress, Token);
            var layout = nl.GetLayout().Result;
            if (layout != null) Layout = layout;
            Scale = 1;
            DataUtil.InsertCollection<NanoData>("leaves", this);
        }
    }
}