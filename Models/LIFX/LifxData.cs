using System;
using System.Collections.Generic;
using System.Linq;
using LifxNet;
using Newtonsoft.Json;

namespace HueDream.Models.LIFX {
    public class LifxData {

        public LifxData() {
            
        }

        public LifxData(LightBulb b) {
            HostName = b.HostName;
            Service = b.Service;
            Port = (int) b.Port;
            MacAddress = b.MacAddress;
        }
        [JsonProperty] 
        public string HostName { get; internal set; }

        [JsonProperty] 
        public byte Service { get; internal set; }

        [JsonProperty] 
        public int Port { get; internal set; }
        [JsonProperty] 
        internal DateTime LastSeen { get; set; }

        [JsonProperty] 
        public byte[] MacAddress { get; internal set; }
        [JsonProperty] 
        public double Hue { get; set; }
        [JsonProperty] 
        public double Saturation { get; set; }
        [JsonProperty] 
        public double Brightness { get; set; }
        [JsonProperty] 
        public int Kelvin { get; set; }
        [JsonProperty] 
        public bool Power { get; set; }
        [JsonProperty] 
        public int SectorMapping { get; set; }
    }
}