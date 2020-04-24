using System;
using System.Collections.Generic;
using HueDream.Models.Util;
using Newtonsoft.Json;

namespace HueDream.Models.DreamScreen.Devices {
    [Serializable]
    public class SideKick : BaseDevice {
        private const string DeviceTag = "SideKick";
        private static readonly byte[] RequiredEspFirmwareVersion = {3, 1};
        public static readonly byte[] DefaultSectorAssignment = {1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 0, 0, 0};

        public SideKick() { }

        public SideKick(string ipAddress) : base(ipAddress) {
            Name = DeviceTag;
            EspFirmwareVersion = RequiredEspFirmwareVersion;
            SectorData = new byte[] {0};
            SectorAssignment = DefaultSectorAssignment;
            IsDemo = false;
            ProductId = 3;
            Tag = DeviceTag;
            GroupName = "unassigned";
        }

        [JsonProperty] private byte[] EspFirmwareVersion { get; set; }

        public override void SetDefaults() {
            
        }

        [JsonProperty] public int[] flexSetup { get; set; }

        [JsonProperty] private bool IsDemo { get; set; }

        [JsonProperty] private byte[] SectorAssignment { get; set; }

        [JsonProperty] private byte[] SectorData { get; set; }

        public override void ParsePayload(byte[] payload) {
            if (payload is null) throw new ArgumentNullException(nameof(payload));

            var name = ByteUtils.ExtractString(payload, 0, 16);
            if (name.Length == 0) name = DeviceTag;
            Name = name;
            var groupName = ByteUtils.ExtractString(payload, 16, 32);
            if (groupName.Length == 0) groupName = "unassigned";
            GroupName = groupName;
            GroupNumber = payload[32];
            Mode = payload[33];
            Brightness = payload[34];
            AmbientColor = ByteUtils.ExtractString(payload, 35, 38);
            Saturation = ByteUtils.ExtractString(payload, 38, 41);
            FadeRate = payload[41];
            SectorAssignment = ByteUtils.ExtractBytes(payload, 42, 57);
            EspFirmwareVersion = ByteUtils.ExtractBytes(payload, 57, 59);
            if (payload.Length == 62) {
                AmbientModeType = payload[59];
                AmbientShowType = payload[60];
            }
        }

        public override byte[] EncodeState() {
            LogUtil.Write($"Encoding state: {Name}, {GroupName}, {GroupNumber}, {Mode}, {Brightness}, {AmbientColor}, {Saturation}, {FadeRate}, {RequiredEspFirmwareVersion}, {AmbientModeType}, {AmbientShowType}");
            var response = new List<byte>();
            response.AddRange(ByteUtils.StringBytePad(Name, 16));
            response.AddRange(ByteUtils.StringBytePad(GroupName, 16));
            response.Add(ByteUtils.IntByte(GroupNumber));
            response.Add(ByteUtils.IntByte(Mode));
            response.Add(ByteUtils.IntByte(Brightness));
            response.AddRange(ByteUtils.StringBytes(AmbientColor));
            response.AddRange(ByteUtils.StringBytes(Saturation));
            response.Add(ByteUtils.IntByte(FadeRate));
            // Sector Data
            response.AddRange(new byte[15]);
            response.AddRange(EspFirmwareVersion);
            response.Add(ByteUtils.IntByte(AmbientModeType));
            response.Add(ByteUtils.IntByte(AmbientShowType));
            // Type
            response.Add(0x03);
            return response.ToArray();
        }
    }
}