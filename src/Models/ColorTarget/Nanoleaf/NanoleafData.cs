using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Glimmr.Models.Util;
using Nanoleaf.Client.Models.Responses;
using Newtonsoft.Json;
using Info = Nanoleaf.Client.Models.Responses.Info;

namespace Glimmr.Models.ColorTarget.Nanoleaf {
    [Serializable]
    public class NanoleafData : IColorTargetData {
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

            if (Layout == null) {
                Layout = new NanoLayout();
            }
            
        }

        public NanoleafData(Info dn) {
            Id = dn.SerialNumber;
            Name = dn.Name;
            Version = dn.FirmwareVersion;
            IpAddress = IpUtil.GetIpFromHost(Name).ToString();
            Tag = "Nanoleaf";
            if (Layout == null) {
                Layout = new NanoLayout();
            }
        }

        // Copy data from an existing leaf into this leaf...don't insert
        public string LastSeen { get; set; }

        public void UpdateFromDiscovered(IColorTargetData data) {
            var existingLeaf = (NanoleafData) data;
            if (existingLeaf == null) throw new ArgumentException("Invalid nano data!");
            // Grab the new leaf layout
            MergeLayout(existingLeaf.Layout);
            Tag = "Nanoleaf";
        }

        public SettingsProperty[] KeyProperties { get; set; } = {
            new("custom", "nanoleaf", "")
        };

        public void MergeLayout(NanoLayout newLayout) {
            if (newLayout == null) throw new ArgumentException("Invalid argument.");
            if (Layout == null) {
                Layout = newLayout;
                return;
            }

            var posData = new NanoPanelLayout[newLayout.PositionData.Length];
            // Loop through each panel in the new position data, find existing info and copy
            for (var i = 0; i < newLayout.NanoPositionData.Length; i++) {
                var nl = newLayout.NanoPositionData[i];
                foreach (var el in Layout.NanoPositionData.Where(s => s.PanelId == nl.PanelId)) {
                    nl.TargetSector = el.TargetSector;
                }
                posData[i] = nl;
            }

            Layout.NumPanels = newLayout.NumPanels;
            Layout.SideLength = newLayout.SideLength;
            Layout.NanoPositionData = posData;
        }


        public string Name { get; set; }
        public string Id { get; set; }
        public string Tag { get; set; }
        public string IpAddress { get; set; }
        public int Brightness { get; set; }
        public bool Enable { get; set; }
        
    }
    [Serializable]
    public class NanoLayout : Layout {
        [JsonProperty] public NanoPanelLayout[] NanoPositionData { get; set; } = Array.Empty<NanoPanelLayout>();

        public NanoLayout() {
        }
        public NanoLayout(Layout layout) {
            NumPanels = layout.NumPanels;
            SideLength = layout.SideLength;
            PositionData = layout.PositionData;
            NanoPositionData = PositionData.Select(pos => new NanoPanelLayout(pos)).ToArray();
        }

    }

    [Serializable]
    public class NanoPanelLayout : PanelLayout {

        [DefaultValue(-1)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int TargetSector { get; set; } = -1;

        public NanoPanelLayout() {
            
        }
        public NanoPanelLayout(PanelLayout layout) {
            PanelId = layout.PanelId;
            X = layout.X;
            Y = layout.Y;
            O = layout.O;
            ShapeType = layout.ShapeType;
        }
    }
}