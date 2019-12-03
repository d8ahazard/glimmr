using System;
using System.Runtime.Serialization;

namespace HueDream.DreamScreen.Devices {
    [Serializable]
    public abstract class BaseDevice : IDreamDevice {

        public string Tag { get; set; }
        [DataMember]
        public string AmbientColor { get; set; }
        public int AmbientModeType { get; set; }
        public int AmbientShowType { get; set; }
        public int Brightness { get; set; }
        public string BroadcastIP { get; set; }

        //private readonly byte[] espSerialNumber = new byte[] { 0, 0 };
        public int FadeRate { get; set; }
        public string Name { get; set; }
        public string GroupName { get; set; }
        public int GroupNumber { get; set; }
        public string IpAddress { get; set; }
        public int Mode { get; set; }
        public int ProductId { get; set; }
        public string Saturation { get; set; }

        
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


        public BaseDevice(string address) {
            IpAddress = address;
        }

        public abstract void ParsePayload(byte[] payload);
        public abstract byte[] EncodeState();
    }

}