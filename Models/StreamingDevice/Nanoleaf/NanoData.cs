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
        [JsonProperty] public string IpV4Address { get; set; }
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


        public void CopyExisting(NanoData leaf) {
            if (leaf == null) throw new ArgumentException("Invalid nanodata!");
            Token = leaf.Token;
            X = leaf.X;
            Y = leaf.Y;
            Scale = 1;
            Rotation = leaf.Rotation;
            if (leaf.Brightness != 0)  Brightness = leaf.Brightness;
            RefreshLeaf();
            var newL = MergeLayouts(leaf.Layout, Layout);
            Layout = newL;
        }

        private NanoLayout MergeLayouts(NanoLayout source, NanoLayout dest) {
            var output = new NanoLayout();
            if (source == null) {
                LogUtil.Write("Source is null, returning.");
                return output;
            }
            
            if (source.PositionData == null) {
                return output;
            }

            output.NumPanels = source.NumPanels;
            // Loop through "old" data, copy sector mappings if found
            foreach (var s in source.PositionData) {
                var sId = s.PanelId;
                foreach (var d in Layout.PositionData.Where(d => d.PanelId == sId)) {
                    d.Sector = s.Sector;
                    output.PositionData.Add(s);
                }
            }

            return output;
        }

        public void RefreshLeaf() {
            if (Token == null) return;
            LogUtil.Write("Refreshing leaf...");
            using var nl = new NanoGroup(IpV4Address, Token);
            var layout = nl.GetLayout().Result;
            LogUtil.Write("New layout retrieved: " + JsonConvert.SerializeObject(layout));
            if (layout != null) Layout = layout;
            Scale = 1;
            DataUtil.InsertCollection<NanoData>("leaves", this);
        }
    }
}