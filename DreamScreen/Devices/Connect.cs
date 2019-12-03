using HueDream.Util;
using System;
using System.Collections.Generic;

namespace HueDream.DreamScreen.Devices {
    public class Connect : BaseDevice {
        public const string DefaultName = "Connect";
        public static readonly byte[] DefaultSectorAssignment = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 0, 0, 0 };
        public static readonly List<int> RemoteCommandValues = new List<int> { Convert.ToInt32(1836103725), Convert.ToInt32(1342658845), Convert.ToInt32(1339163517), Convert.ToInt32(459778273), Convert.ToInt32(720925977), Convert.ToInt32(1114347969), Convert.ToInt32(30463369), Convert.ToInt32(-1573044211) };
        public static readonly List<string> IrCommands = new List<string> { "Undefined", "Mode Toggle", "Mode Sleep", "Mode Video", "Mode Audio", "Mode Ambient", "Brightness Up 10%", "Brightness Down 10%", "HDMI Toggle", "HDMI 1", "HDMI 2", "HDMI 3", "Ambient Scene Toggle" };
        public const int LightCount = 10;
        private static readonly byte[] requiredEspFirmwareVersion = new byte[] { 0, 4 };
        private const string tag = "Connect";
        public int AmbientLightAutoAdjustEnabled { get; set; }
        public int DisplayAnimationEnabled { get; set; }
        private byte[] espFirmwareVersion;
        public int HdmiInput { get; set; }
        /* access modifiers changed from: private */
        public bool HueLifxSettingsReceived { get; set; }
        public int IrEnabled { get; set; }
        public int IrLearningMode { get; set; }
        public byte[] IrManifest { get; set; }
        public bool isDemo { get; set; }
        public int microphoneAudioBroadcastEnabled { get; set; }
        public byte[] sectorData { get; set; }
        public string ThingName { get; set; }

        public Connect(string ipAddress) : base(ipAddress) {
            Tag = tag;
            espFirmwareVersion = requiredEspFirmwareVersion;
            HdmiInput = 0;
            DisplayAnimationEnabled = 0;
            AmbientLightAutoAdjustEnabled = 0;
            microphoneAudioBroadcastEnabled = 0;
            IrEnabled = 1;
            IrLearningMode = 0;
            IrManifest = new byte[40];
            ThingName = "";
            sectorData = DefaultSectorAssignment;
            isDemo = false;
            HueLifxSettingsReceived = false;
            ProductId = 4;
            Name = "Connect";
            GroupName = "unassigned";
        }

        public override void ParsePayload(byte[] payload) {
            if (payload != null) {
                try {
                    string name = ByteUtils.ExtractString(payload, 0, 16);
                    if (name.Length == 0) {
                        name = "Connect";
                    }
                    Name = name;
                    string groupName = ByteUtils.ExtractString(payload, 16, 32);
                    if (groupName.Length == 0) {
                        groupName = "Group";
                    }
                    GroupName = groupName;
                } catch (IndexOutOfRangeException) {
                    Console.WriteLine($"Index out of range, payload length is {payload.Length}.");
                }
                GroupNumber = payload[32];
                Mode = payload[33];
                Brightness = payload[34];
                AmbientColor = (ByteUtils.ExtractString(payload, 35, 38));
                Saturation = (ByteUtils.ExtractString(payload, 38, 41));
                FadeRate = payload[41];
                espFirmwareVersion = ByteUtils.ExtractBytes(payload, 57, 59);
                AmbientModeType = payload[59];
                AmbientShowType = payload[60];
                HdmiInput = payload[61];
                DisplayAnimationEnabled = payload[62];
                AmbientLightAutoAdjustEnabled = payload[63];
                microphoneAudioBroadcastEnabled = payload[64];
                IrEnabled = payload[65];
                IrLearningMode = payload[66];
                IrManifest = ByteUtils.ExtractBytes(payload, 67, 107);
                if (payload.Length > 115) {
                    try {
                        ThingName = ByteUtils.ExtractString(payload, 115, 178);
                    } catch (IndexOutOfRangeException) {
                        ThingName = "";
                    }
                }
            }
        }

        public override byte[] EncodeState() {
            List<byte> response = new List<byte>();
            response.AddRange(ByteUtils.StringBytePad(Name, 16));
            response.AddRange(ByteUtils.StringBytePad(GroupName, 16));
            response.Add(ByteUtils.IntByte(GroupNumber));
            response.Add(ByteUtils.IntByte(Mode));
            response.Add(ByteUtils.IntByte(Brightness));
            response.AddRange(ByteUtils.StringBytes(AmbientColor));
            response.AddRange(ByteUtils.StringBytes(Saturation));
            response.Add(ByteUtils.IntByte(FadeRate));
            response.AddRange(espFirmwareVersion);
            response.Add(ByteUtils.IntByte(AmbientModeType));
            response.Add(ByteUtils.IntByte(AmbientShowType));
            response.Add(ByteUtils.IntByte(HdmiInput));
            response.Add(ByteUtils.IntByte(DisplayAnimationEnabled));
            response.Add(ByteUtils.IntByte(AmbientLightAutoAdjustEnabled));
            response.Add(ByteUtils.IntByte(microphoneAudioBroadcastEnabled));
            response.Add(ByteUtils.IntByte(IrEnabled));
            response.Add(ByteUtils.IntByte(IrLearningMode));
            response.AddRange(IrManifest);
            response.AddRange(ByteUtils.StringBytePad(ThingName, 63));
            // Type
            response.Add(0x04);

            return response.ToArray();
        }
    }

}