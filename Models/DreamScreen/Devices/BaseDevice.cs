using System;
using System.Runtime.Serialization;
using HueDream.DreamScreen.Devices;

namespace HueDream.Models.DreamScreen.Devices {
    [Serializable, DataContract]
    public abstract class BaseDevice : IDreamDevice {
        [DataMember] private readonly byte[] espSerialNumber = {0, 0};


        protected BaseDevice(string address) {
            IpAddress = address;
        }

        protected BaseDevice() { }

        [DataMember] public int AmbientShowType { get; set; }

        [DataMember] public int FadeRate { get; set; }

        [DataMember] public string IpAddress { get; set; }

        [DataMember] public int ProductId { get; set; }

        [DataMember] public string Tag { get; set; }

        [DataMember] public string AmbientColor { get; set; }

        [DataMember] public int AmbientModeType { get; set; }
        [DataMember] public int Brightness { get; set; }
        [DataMember] public string Name { get; set; }
        [DataMember] public string GroupName { get; set; }
        [DataMember] public int GroupNumber { get; set; }
        [DataMember] public int Mode { get; set; }
        [DataMember] public string Saturation { get; set; }

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