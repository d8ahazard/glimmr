using System;
using System.ComponentModel;
using System.Globalization;
using Glimmr.Models.Util;
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
        [JsonProperty] public TileLayout Layout { get; set; }

       
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
                Layout = new TileLayout();
            }
            
        }

        public NanoleafData(Info dn) {
            Id = dn.SerialNumber;
            Name = dn.Name;
            Version = dn.FirmwareVersion;
            IpAddress = IpUtil.GetIpFromHost(Name).ToString();
            Tag = "Nanoleaf";
            if (Layout == null) {
                Layout = new TileLayout();
            }
        }

        // Copy data from an existing leaf into this leaf...don't insert
        public string LastSeen { get; set; }

        public void UpdateFromDiscovered(IColorTargetData data) {
            var existingLeaf = (NanoleafData) data;
            if (existingLeaf == null) throw new ArgumentException("Invalid nano data!");
            // Grab the new leaf layout
            Layout.MergeLayout(existingLeaf.Layout);
            Tag = "Nanoleaf";
            IpAddress = data.IpAddress;
        }

        public SettingsProperty[] KeyProperties { get; set; } = {
            new("custom", "nanoleaf", ""),
            new("FrameDelay", "text", "Frame Delay")
        };

      


        public string Name { get; set; }
        public string Id { get; set; }
        public string Tag { get; set; }
        public string IpAddress { get; set; }
        public int Brightness { get; set; }
        public int FrameDelay { get; set; }
        public bool Enable { get; set; }
        
    }
    
}