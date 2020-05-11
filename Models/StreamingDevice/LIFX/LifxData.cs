using System;
using System.ComponentModel;
using LifxNet;
using Newtonsoft.Json;

namespace HueDream.Models.StreamingDevice.LIFX {
    public class LifxData {
        public LifxData() {
        }

        public LifxData(LightBulb b) {
            if (b == null) throw new ArgumentException("Invalid bulb data.");
            HostName = b.HostName;
            Id = b.MacAddressName;
            Service = b.Service;
            Port = (int) b.Port;
            MacAddress = b.MacAddress;
            MacAddressString = b.MacAddressName;
        }

        [JsonProperty] public string HostName { get; internal set; }
        [JsonProperty] public string Id { get; internal set; }

        [JsonProperty] public byte Service { get; internal set; }

        [JsonProperty] public int Port { get; internal set; }
        [JsonProperty] internal DateTime LastSeen { get; set; }

        [JsonProperty] public byte[] MacAddress { get; internal set; }

        [JsonProperty] public string MacAddressString { get; internal set; }

        [JsonProperty] public ushort Hue { get; set; }
        [JsonProperty] public ushort Saturation { get; set; }
        [JsonProperty] public ushort Brightness { get; set; }
        [JsonProperty] public ushort Kelvin { get; set; }
        [JsonProperty] public bool Power { get; set; }
        [JsonProperty] public int SectorMapping { get; set; }
        [DefaultValue(100)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int MaxBrightness { get; set; }

    }
}