namespace HueDream.DreamScreen.Devices {
    using global::HueDream.Util;
    using System;
    using System.Collections.Generic;

    public class SideKick : BaseDevice {
        private static readonly byte[] requiredEspFirmwareVersion = new byte[] { 3, 1 };
        private const string tag = "SideKick";
        public byte[] espFirmwareVersion { get; set; }
        public bool isDemo { get; set; }
        public byte[] sectorAssignment { get; set; }
        public byte[] sectorData { get; set; }

        public SideKick(string ipAddress) : base(ipAddress) {
            espFirmwareVersion = requiredEspFirmwareVersion;
            sectorData = new byte[] { 0 };
            sectorAssignment = new byte[0];
            isDemo = false;
            ProductId = 3;
            Name = tag;
            Tag = tag;
        }

        public override void ParsePayload(byte[] payload) {
            Console.WriteLine("parsePayload");
            try {
                string name = ByteUtils.ExtractString(payload, 0, 16);
                if (name.Length == 0) {
                    name = tag;
                }
                this.Name = name;
                string groupName = ByteUtils.ExtractString(payload, 16, 32);
                if (groupName.Length == 0) {
                    groupName = "unassigned";
                }
                this.GroupName = groupName;
            } catch (Exception) {
            }
            GroupNumber = payload[32];
            Mode = payload[33];
            Brightness = payload[34];
            AmbientColor = ByteUtils.ExtractInt(payload, 35, 38);
            Saturation = ByteUtils.ExtractInt(payload, 38, 41);
            FadeRate = payload[41];
            sectorAssignment = ByteUtils.ExtractBytes(payload, 42, 57);
            espFirmwareVersion = ByteUtils.ExtractBytes(payload, 57, 59);
            if (payload.Length == 62) {
                AmbientModeType = payload[59];
                AmbientShowType = payload[60];
            }
        }

        public override byte[] EncodeState() {
            List<byte> response = new List<byte>();
            response.AddRange(ByteUtils.StringBytePad(Name, 16));
            response.AddRange(ByteUtils.StringBytePad(GroupName, 16));
            response.Add(ByteUtils.IntByte(GroupNumber));
            response.Add(ByteUtils.IntByte(Mode));
            response.Add(ByteUtils.IntByte(Brightness));

            response.AddRange(ByteUtils.IntBytes(AmbientColor));
            response.AddRange(ByteUtils.IntBytes(Saturation));
            response.Add(ByteUtils.IntByte(FadeRate));
            // Sector Data
            response.AddRange(new byte[15]);
            response.AddRange(espFirmwareVersion);
            response.Add(ByteUtils.IntByte(AmbientModeType));
            response.Add(ByteUtils.IntByte(AmbientShowType));
            // Type
            response.Add(0x03);            
            return response.ToArray();
        }
    }

}