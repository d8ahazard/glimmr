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

        public NanoleafData(Info dn) {
            Id = dn.SerialNumber;
            Name = dn.Name;
            Version = dn.FirmwareVersion;
            IpAddress = IpUtil.GetIpFromHost(Name).ToString();
            Tag = "Nanoleaf";
        }

        // Copy data from an existing leaf into this leaf...don't insert
        public NanoleafData CopyExisting(NanoleafData existingLeaf) {
            if (existingLeaf == null) throw new ArgumentException("Invalid nano data!");
            Token = existingLeaf.Token;
            X = existingLeaf.X;
            Y = existingLeaf.Y;
            Scale = 1;
            Rotation = existingLeaf.Rotation;
            Enable = existingLeaf.Enable;
            if (existingLeaf.Brightness != 0)  Brightness = existingLeaf.Brightness;
            // Grab the new leaf layout
            RefreshLeaf();
            Tag = "Nanoleaf";
            
            return this;
        }

        private NanoLayout MergeLayout(NanoLayout newLayout) {
            if (newLayout == null) throw new ArgumentException("Invalid argument.");
            if (Layout == null) return newLayout;
            var posData = new List<PanelLayout>();
            var output = newLayout;
            
            if (Layout.PositionData == null) {
                return output;
            }

            // Loop through each panel in the new position data, find existing info and copy
            foreach (var nl in newLayout.PositionData) {
                foreach (var el in Layout.PositionData.Where(s => s.PanelId == nl.PanelId)) {
                    nl.TargetSector = el.TargetSector;
                }
                posData.Add(nl);
            }
            output.PositionData = posData;
            return output;
        }

        public void RefreshLeaf() {
            if (Token == null) {
                Log.Debug("NO TOKEN!");
                return;
            }
            using var nl = new NanoleafDevice(this, new HttpClient());
            var layout = nl.GetLayout().Result;
            if (layout != null) {
                Layout = MergeLayout(layout);
                Log.Debug("No, fucking really. Layout set: " + JsonConvert.SerializeObject(Layout));
            } else {
                Log.Debug("Layout is null.");
            }
            
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