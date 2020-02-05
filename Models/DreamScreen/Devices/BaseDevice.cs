using Newtonsoft.Json;
using System;
using System.Runtime.Serialization;

namespace HueDream.Models.DreamScreen.Devices {
    [Serializable, DataContract]
    public abstract class BaseDevice : IDreamDevice {
        [DataMember] private readonly byte[] espSerialNumber = {0, 0};


        protected BaseDevice(string address) {
            IpAddress = address;
        }

        protected BaseDevice() { }

        [DataMember][JsonProperty] public int AmbientShowType { get; set; }
        [DataMember][JsonProperty] public int FadeRate { get; set; }
        [DataMember][JsonProperty] public string IpAddress { get; set; }
        [DataMember] [JsonProperty] public int ProductId { get; set; }
        [DataMember] [JsonProperty] public string Tag { get; set; }
        [DataMember] [JsonProperty] public string AmbientColor { get; set; }
        [DataMember] [JsonProperty] public int AmbientModeType { get; set; }
        [DataMember] [JsonProperty] public int[] flexSetup { get; set; }
        [DataMember] [JsonProperty] public int Brightness { get; set; }
        [DataMember] [JsonProperty] public string Name { get; set; }
        [DataMember] [JsonProperty] public string GroupName { get; set; }
        [DataMember] [JsonProperty] public int GroupNumber { get; set; }
        [DataMember] [JsonProperty] public int Mode { get; set; }
        [DataMember] [JsonProperty] public string Id { get; set; }
        [DataMember] [JsonProperty] public string Saturation { get; set; }

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