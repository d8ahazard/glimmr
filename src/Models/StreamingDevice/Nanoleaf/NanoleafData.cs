using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using Glimmr.Models.Util;
using Newtonsoft.Json;
using Serilog;
using Info = Nanoleaf.Client.Models.Responses.Info;

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
        public NanoleafData CopyExisting(NanoleafData existingLeaf) {
            if (existingLeaf == null) throw new ArgumentException("Invalid nano data!");
            Token = existingLeaf.Token;
            Enable = existingLeaf.Enable;
            if (existingLeaf.Brightness != 0)  Brightness = existingLeaf.Brightness;
            // Grab the new leaf layout
            MergeLayout(existingLeaf.Layout);
            Tag = "Nanoleaf";
            
            return this;
        }

        public void MergeLayout(NanoLayout newLayout) {
            if (newLayout == null) throw new ArgumentException("Invalid argument.");
            if (Layout == null) {
                Layout = newLayout;
                return;
            }

            var posData = new PanelLayout[newLayout.PositionData.Length];
            // Loop through each panel in the new position data, find existing info and copy
            for (var i = 0; i < newLayout.PositionData.Length; i++) {
                var nl = newLayout.PositionData[i];
                foreach (var el in Layout.PositionData.Where(s => s.PanelId == nl.PanelId)) {
                    nl.TargetSector = el.TargetSector;
                }
                posData[i] = nl;
            }

            Layout.PositionData = posData;
        }

        
    }
    [Serializable]
    public class NanoLayout {
        [JsonProperty] public int NumPanels { get; set; }
        [JsonProperty] public int SideLength { get; set; } = 1;
        [JsonProperty] public PanelLayout[] PositionData { get; set; } = Array.Empty<PanelLayout>();

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