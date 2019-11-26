using HueDream.Util;
using System;
using System.Collections.Generic;
using System.Text;

namespace HueDream.DreamScreen.Devices {
    public class DreamScreen : BaseDevice {
        private const string bootloaderKey = "Ka";
        private static readonly byte[] requiredEspFirmwareVersion = new byte[] { 1, 6 };
        private static readonly byte[] requiredPicVersionNumber = new byte[] { 1, 7 };
        private const string resetKey = "sA";
        private const string tag = "DreamScreen";
        private byte[] appMusicData;
        private int bootState;
        private int cecPassthroughEnable;
        private int cecPowerEnable;
        private int cecSwitchingEnable;
        private int colorBoost;
        internal byte[] espFirmwareVersion;
        private int[] flexSetup;
        internal byte hdmiActiveChannels;
        private int hdmiInput;
        internal string hdmiInputName1;
        internal string hdmiInputName2;
        internal string hdmiInputName3;
        private int hdrToneRemapping;
        private int hpdEnable;
        private int indicatorLightAutoOff;
        internal bool isDemo;
        private int letterboxingEnable;
        private int[] minimumLuminosity;
        private int[] musicModeColors;
        private int musicModeSource;
        private int musicModeType;
        private int[] musicModeWeights;
        internal byte[] picVersionNumber;
        private int pillarboxingEnable;
        private int sectorBroadcastControl;
        private int sectorBroadcastTiming;
        private int skuSetup;
        private int usbPowerEnable;
        private int videoFrameDelay;
        private byte zones;
        private int[] zonesBrightness;

        public DreamScreen(string ipAddress) : base(ipAddress) {
            Tag = tag;
            espFirmwareVersion = requiredEspFirmwareVersion;
            picVersionNumber = requiredPicVersionNumber;
            zones = 15;
            zonesBrightness = new int[] { 255, 255, 255 };
            musicModeType = 0;
            musicModeColors = new int[] { 255, 255, 255 };
            musicModeWeights = new int[] { 100, 100, 100 };
            minimumLuminosity = new int[] { 0, 0, 0 };
            indicatorLightAutoOff = 1;
            usbPowerEnable = 0;
            sectorBroadcastControl = 0;
            sectorBroadcastTiming = 1;
            hdmiInput = 0;
            musicModeSource = 0;
            appMusicData = new byte[] { 0, 0, 0 };
            cecPassthroughEnable = 1;
            cecSwitchingEnable = 1;
            hpdEnable = 1;
            videoFrameDelay = 0;
            letterboxingEnable = 0;
            pillarboxingEnable = 0;
            hdmiActiveChannels = 0;
            colorBoost = 0;
            cecPowerEnable = 0;
            flexSetup = new int[] { 8, 16, 48, 0, 7, 0 };
            skuSetup = 0;
            hdrToneRemapping = 0;
            bootState = 0;
            isDemo = false;
            ProductId = 1;
            Name = "DreamScreen HD";
            try {
                hdmiInputName1 = "HDMI 1";
                hdmiInputName2 = "HDMI 2";
                hdmiInputName3 = "HDMI 3";
            } catch (Exception) {
            }
        }


        public override void ParsePayload(byte[] payload) {
            Console.WriteLine("parsePayload: " + payload.Length);
            try {
                string name1 = ByteUtils.ExtractString(payload, 0, 16);
                if (name1.Length == 0) {
                    name1 = tag;
                }
                Name = name1;
                string groupName1 = ByteUtils.ExtractString(payload, 16, 32);
                if (groupName1.Length == 0) {
                    groupName1 = "Group";
                }
                GroupName = groupName1;
            } catch (Exception) {
            }
            Console.WriteLine("Name");
            GroupNumber = payload[32];
            Mode = payload[33];
            Brightness = payload[34];
            zones = payload[35];
            Console.WriteLine("ZB");
            zonesBrightness = ByteUtils.ExtractInt(payload, 36, 40);
            AmbientColor = ByteUtils.ExtractInt(payload, 40, 43);
            Saturation = ByteUtils.ExtractInt(payload, 43, 46);
            flexSetup = ByteUtils.ExtractInt(payload, 46, 52);
            Console.WriteLine("MMType");
            musicModeType = payload[52];
            musicModeColors = ByteUtils.ExtractInt(payload, 53, 56);
            musicModeWeights = ByteUtils.ExtractInt(payload, 56, 59);
            minimumLuminosity = ByteUtils.ExtractInt(payload, 59, 62);
            AmbientShowType = payload[62];
            FadeRate = payload[63];
            Console.WriteLine("Frate");
            indicatorLightAutoOff = payload[69];
            usbPowerEnable = payload[70];
            sectorBroadcastControl = payload[71];
            sectorBroadcastTiming = payload[72];
            hdmiInput = payload[73];
            musicModeSource = payload[74];
            Console.WriteLine("MModesource");
            hdmiInputName1 = ByteUtils.ExtractString(payload, 75, 91);
            hdmiInputName2 = ByteUtils.ExtractString(payload, 91, 107);
            hdmiInputName3 = ByteUtils.ExtractString(payload, 107, 123);
            cecPassthroughEnable = payload[123];
            cecSwitchingEnable = payload[124];
            hpdEnable = payload[125];
            videoFrameDelay = payload[127];
            letterboxingEnable = payload[128];
            hdmiActiveChannels = payload[129];
            espFirmwareVersion = ByteUtils.ExtractBytes(payload, 130, 132);
            picVersionNumber = ByteUtils.ExtractBytes(payload, 132, 134);
            colorBoost = payload[134];
            if (payload.Length >= 137) {
                cecPowerEnable = payload[135];
            }
            if (payload.Length >= 138) {
                skuSetup = payload[136];
            }
            if (payload.Length >= 139) {
                bootState = payload[137];
            }
            if (payload.Length >= 140) {
                pillarboxingEnable = payload[138];
            }
            if (payload.Length >= 141) {
                hdrToneRemapping = payload[139];
            }
            Console.WriteLine("Parsed");
        }

        public override byte[] EncodeState() {
            List<byte> response = new List<byte>();
            byte[] nByte = ByteUtils.StringBytePad(Name, 16);
            response.AddRange(nByte);
            byte[] gByte = ByteUtils.StringBytePad(GroupName, 16);
            response.AddRange(gByte);
            response.Add(ByteUtils.IntByte(GroupNumber));
            response.Add(ByteUtils.IntByte(Mode));
            response.Add(ByteUtils.IntByte(Brightness));
            response.Add(zones);
            response.AddRange(ByteUtils.IntBytes(zonesBrightness));
            response.AddRange(ByteUtils.IntBytes(AmbientColor));
            response.AddRange(ByteUtils.IntBytes(Saturation));
            response.AddRange(ByteUtils.IntBytes(flexSetup));
            response.Add(ByteUtils.IntByte(musicModeType));
            response.AddRange(ByteUtils.IntBytes(musicModeColors));
            response.AddRange(ByteUtils.IntBytes(musicModeWeights));
            response.AddRange(ByteUtils.IntBytes(minimumLuminosity));
            response.Add(ByteUtils.IntByte(AmbientShowType));
            response.Add(ByteUtils.IntByte(FadeRate));
            response.AddRange(new byte[5]);
            response.Add(ByteUtils.IntByte(indicatorLightAutoOff));
            response.Add(ByteUtils.IntByte(usbPowerEnable));
            response.Add(ByteUtils.IntByte(sectorBroadcastControl));
            response.Add(ByteUtils.IntByte(sectorBroadcastTiming));
            response.Add(ByteUtils.IntByte(hdmiInput));
            response.AddRange(new byte[2]);
            Console.WriteLine("hdmiInterfaces");
            string[] iList = { hdmiInputName1, hdmiInputName2, hdmiInputName3 };
            foreach (string iName in iList) {
                response.AddRange(ByteUtils.StringBytePad(iName, 16));
            }
            response.Add(ByteUtils.IntByte(cecPassthroughEnable));
            response.Add(ByteUtils.IntByte(cecSwitchingEnable));
            response.Add(ByteUtils.IntByte(hpdEnable));
            response.Add(0x00);
            response.Add(ByteUtils.IntByte(videoFrameDelay));
            response.Add(ByteUtils.IntByte(letterboxingEnable));
            response.Add(ByteUtils.IntByte(hdmiActiveChannels));
            response.AddRange(espFirmwareVersion);
            response.AddRange(picVersionNumber);
            response.Add(ByteUtils.IntByte(colorBoost));
            response.Add(ByteUtils.IntByte(cecPowerEnable));
            response.Add(ByteUtils.IntByte(skuSetup));
            response.Add(ByteUtils.IntByte(bootState));
            response.Add(ByteUtils.IntByte(pillarboxingEnable));
            response.Add(ByteUtils.IntByte(hdrToneRemapping));
            Console.WriteLine("Type");
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