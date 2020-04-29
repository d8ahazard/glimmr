using System;
using System.Collections.Generic;
using System.Linq;
using LifxNet;

namespace HueDream.Models.LIFX {
    public class LifxData {
        public LifxData(LightBulb b) {
            HostName = b.HostName;
            Service = b.Service;
            Port = b.Port;
            MacAddress = b.MacAddress;
            var lC = LifxClient.CreateAsync().Result;
            Power = lC.GetLightPowerAsync(b).Result;
            var state = lC.GetLightStateAsync(b).Result;
            Hue = state.Hue;
            Brightness = state.Brightness;
        }
        
        public string HostName { get; internal set; }

        /// <summary>Service ID</summary>
        public byte Service { get; internal set; }

        /// <summary>Service port</summary>
        public uint Port { get; internal set; }

        internal DateTime LastSeen { get; set; }

        /// <summary>Gets the MAC address</summary>
        public byte[] MacAddress { get; internal set; }
        public int Brightness { get; set; }
        public bool Power { get; set; }
        public ushort Hue { get; set; }
    }
}