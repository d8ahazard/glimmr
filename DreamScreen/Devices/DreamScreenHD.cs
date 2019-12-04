using HueDream.Util;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HueDream.DreamScreen.Devices {
    public class DreamScreenHd : BaseDevice {
        private static readonly byte[] RequiredEspFirmwareVersion = { 1, 6 };
        private static readonly byte[] RequiredPicVersionNumber = { 1, 7 };
        private const string DeviceTag = "DreamScreen";
        [JsonProperty] 
        private byte[] appMusicData;
        [JsonProperty] 
        public int BootState { get; set; }
        [JsonProperty] 
        public int CecPassthroughEnable { get; set; }
        [JsonProperty] 
        public int CecPowerEnable { get; set; }
        [JsonProperty] 
        public int CecSwitchingEnable { get; set; }
        [JsonProperty] 
        public int ColorBoost { get; set; }
        [JsonProperty] public byte[] EspFirmwareVersion { get; set; }
        [JsonProperty] private int[] flexSetup;
        [JsonProperty] 
        public byte HdmiActiveChannels { get; set; }
        [JsonProperty] 
        public int HdmiInput { get; set; }
        [JsonProperty] 
        public string HdmiInputName1 { get; set; }
        [JsonProperty] 
        public string HdmiInputName2 { get; set; }
        [JsonProperty] 
        public string HdmiInputName3 { get; set; }
        [JsonProperty] 
        public int HdrToneRemapping { get; set; }
        [JsonProperty] 
        public int HpdEnable { get; set; }
        [JsonProperty] 
        public int IndicatorLightAutoOff { get; set; }
        [JsonProperty]
        public bool IsDemo { get; set; }
        [JsonProperty]
        public int LetterboxingEnable { get; set; }
        [JsonProperty]
        private int[] MinimumLuminosity { get; set; }

        [JsonProperty]
        private int[] musicModeColors;
        [JsonProperty]
        public int MusicModeSource { get; set; }
        [JsonProperty]
        public int MusicModeType { get; set; }
        [JsonProperty] private int[] musicModeWeights;
        [JsonProperty] public byte[] PicVersionNumber { get; set; }
        [JsonProperty] 
        public int PillarboxingEnable { get; set; }
        [JsonProperty] 
        public int SectorBroadcastControl { get; set; }
        [JsonProperty] 
        public int SectorBroadcastTiming { get; set; }
        [JsonProperty] 
        public int SkuSetup { get; set; }
        [JsonProperty] 
        public int UsbPowerEnable { get; set; }
        [JsonProperty] 
        public int VideoFrameDelay { get; set; }
        [JsonProperty] 
        public byte Zones { get; set; }
        [JsonProperty] 
        private int[] ZonesBrightness { get; set; }

        public DreamScreenHd(string ipAddress) : base(ipAddress) {
            Tag = DeviceTag;
            EspFirmwareVersion = RequiredEspFirmwareVersion;
            PicVersionNumber = RequiredPicVersionNumber;
            Zones = 15;
            ZonesBrightness = new[] { 255, 255, 255 };
            MusicModeType = 0;
            musicModeColors = new[] { 255, 255, 255 };
            musicModeWeights = new[] { 100, 100, 100 };
            MinimumLuminosity = new[] { 0, 0, 0 };
            IndicatorLightAutoOff = 1;
            UsbPowerEnable = 0;
            SectorBroadcastControl = 0;
            SectorBroadcastTiming = 1;
            HdmiInput = 0;
            MusicModeSource = 0;
            appMusicData = new byte[] { 0, 0, 0 };
            CecPassthroughEnable = 1;
            CecSwitchingEnable = 1;
            HpdEnable = 1;
            VideoFrameDelay = 0;
            LetterboxingEnable = 0;
            PillarboxingEnable = 0;
            HdmiActiveChannels = 0;
            ColorBoost = 0;
            CecPowerEnable = 0;
            flexSetup = new[] { 8, 16, 48, 0, 7, 0 };
            SkuSetup = 0;
            HdrToneRemapping = 0;
            BootState = 0;
            IsDemo = false;
            ProductId = 1;
            Name = "DreamScreen HD";
            HdmiInputName1 = "HDMI 1";
            HdmiInputName2 = "HDMI 2";
            HdmiInputName3 = "HDMI 3";
        }


        public override void ParsePayload(byte[] payload) {
            if (payload is null) {
                throw new ArgumentNullException(nameof(payload));
            }
            if (payload.Length < 132) {
                throw new ArgumentException($"Payload length is too short: {payload.Length}");
            }
            var name1 = ByteUtils.ExtractString(payload, 0, 16);
            if (name1.Length == 0) {
                name1 = DeviceTag;
            }
            Name = name1;
            var groupName1 = ByteUtils.ExtractString(payload, 16, 32);
            if (groupName1.Length == 0) {
                groupName1 = "Group";
            }
            GroupName = groupName1;
            GroupNumber = payload[32];
            Mode = payload[33];
            Brightness = payload[34];
            Zones = payload[35];
            ZonesBrightness = ByteUtils.ExtractInt(payload, 36, 40);
            AmbientColor = ByteUtils.ExtractString(payload, 40, 43);
            Saturation = ByteUtils.ExtractString(payload, 43, 46);
            flexSetup = ByteUtils.ExtractInt(payload, 46, 52);
            MusicModeType = payload[52];
            musicModeColors = ByteUtils.ExtractInt(payload, 53, 56);
            musicModeWeights = ByteUtils.ExtractInt(payload, 56, 59);
            MinimumLuminosity = ByteUtils.ExtractInt(payload, 59, 62);
            AmbientShowType = payload[62];
            FadeRate = payload[63];
            IndicatorLightAutoOff = payload[69];
            UsbPowerEnable = payload[70];
            SectorBroadcastControl = payload[71];
            SectorBroadcastTiming = payload[72];
            HdmiInput = payload[73];
            MusicModeSource = payload[74];
            HdmiInputName1 = ByteUtils.ExtractString(payload, 75, 91);
            HdmiInputName2 = ByteUtils.ExtractString(payload, 91, 107);
            HdmiInputName3 = ByteUtils.ExtractString(payload, 107, 123);
            CecPassthroughEnable = payload[123];
            CecSwitchingEnable = payload[124];
            HpdEnable = payload[125];
            VideoFrameDelay = payload[127];
            LetterboxingEnable = payload[128];
            HdmiActiveChannels = payload[129];
            EspFirmwareVersion = ByteUtils.ExtractBytes(payload, 130, 132);
            PicVersionNumber = ByteUtils.ExtractBytes(payload, 132, 134);
            ColorBoost = payload[134];
            if (payload.Length >= 137) {
                CecPowerEnable = payload[135];
            }
            if (payload.Length >= 138) {
                SkuSetup = payload[136];
            }
            if (payload.Length >= 139) {
                BootState = payload[137];
            }
            if (payload.Length >= 140) {
                PillarboxingEnable = payload[138];
            }
            if (payload.Length >= 141) {
                HdrToneRemapping = payload[139];
            }
        }

        public override byte[] EncodeState() {
            var response = new List<byte>();
            var nByte = ByteUtils.StringBytePad(Name, 16);
            response.AddRange(nByte);
            var gByte = ByteUtils.StringBytePad(GroupName, 16);
            response.AddRange(gByte);
            response.Add(ByteUtils.IntByte(GroupNumber));
            response.Add(ByteUtils.IntByte(Mode));
            response.Add(ByteUtils.IntByte(Brightness));
            response.Add(Zones);
            response.AddRange(ByteUtils.IntBytes(ZonesBrightness));
            response.AddRange(ByteUtils.StringBytes(AmbientColor));
            response.AddRange(ByteUtils.StringBytes(Saturation));
            response.AddRange(ByteUtils.IntBytes(flexSetup));
            response.Add(ByteUtils.IntByte(MusicModeType));
            response.AddRange(ByteUtils.IntBytes(musicModeColors));
            response.AddRange(ByteUtils.IntBytes(musicModeWeights));
            response.AddRange(ByteUtils.IntBytes(MinimumLuminosity));
            response.Add(ByteUtils.IntByte(AmbientShowType));
            response.Add(ByteUtils.IntByte(FadeRate));
            response.AddRange(new byte[5]);
            response.Add(ByteUtils.IntByte(IndicatorLightAutoOff));
            response.Add(ByteUtils.IntByte(UsbPowerEnable));
            response.Add(ByteUtils.IntByte(SectorBroadcastControl));
            response.Add(ByteUtils.IntByte(SectorBroadcastTiming));
            response.Add(ByteUtils.IntByte(HdmiInput));
            response.AddRange(new byte[2]);
            string[] iList = { HdmiInputName1, HdmiInputName2, HdmiInputName3 };
            foreach (var iName in iList) {
                response.AddRange(ByteUtils.StringBytePad(iName, 16));
            }
            response.Add(ByteUtils.IntByte(CecPassthroughEnable));
            response.Add(ByteUtils.IntByte(CecSwitchingEnable));
            response.Add(ByteUtils.IntByte(HpdEnable));
            response.Add(0x00);
            response.Add(ByteUtils.IntByte(VideoFrameDelay));
            response.Add(ByteUtils.IntByte(LetterboxingEnable));
            response.Add(ByteUtils.IntByte(HdmiActiveChannels));
            response.AddRange(EspFirmwareVersion);
            response.AddRange(PicVersionNumber);
            response.Add(ByteUtils.IntByte(ColorBoost));
            response.Add(ByteUtils.IntByte(CecPowerEnable));
            response.Add(ByteUtils.IntByte(SkuSetup));
            response.Add(ByteUtils.IntByte(BootState));
            response.Add(ByteUtils.IntByte(PillarboxingEnable));
            response.Add(ByteUtils.IntByte(HdrToneRemapping));
            // Device type
            if (Tag == "DreamScreen") {
                response.Add(0x01);
            } else if (Tag == "DreamScreen4K") {
                response.Add(0x02);
            } else {
                response.Add(0x07);
            }

            return response.ToArray();

        }
    }

}