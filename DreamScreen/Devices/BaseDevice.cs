using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace HueDream.DreamScreen.Devices {
    [Serializable]
    public abstract class BaseDevice : IDreamDevice {
        [JsonProperty] private readonly byte[] espSerialNumber = {0, 0};


        protected BaseDevice(string address) {
            IpAddress = address;
        }

        public int AmbientShowType { get; set; }

        [JsonProperty] public int FadeRate { get; set; }

        [JsonProperty] public string IpAddress { get; set; }

        [JsonProperty] public int ProductId { get; set; }

        public string Tag { get; set; }

        [DataMember] public string AmbientColor { get; set; }

        public int AmbientModeType { get; set; }
        public int Brightness { get; set; }
        public string Name { get; set; }
        public string GroupName { get; set; }
        public int GroupNumber { get; set; }
        public int Mode { get; set; }
        public string Saturation { get; set; }

        public abstract void ParsePayload(byte[] payload);
        public abstract byte[] EncodeState();


        public void Initialize() {
            GroupName = "unassigned";
            GroupNumber = 0;
            Saturation = "FFFFFF";
            AmbientColor = "000000";
            AmbientModeType = 0;
            AmbientShowType = 0;
            Brightness = 100;
            FadeRate = 4;
            Mode = 0;
            // = "Light";
        }
    }
}