using System;
using System.Collections.Generic;
using System.Linq;

namespace Glimmr.Models.Util {
    public static class MsgUtils {
        private static readonly byte[] Crc8Table = {
            0x00, 0x07, 0x0E, 0x09, 0x1C, 0x1B,
            0x12, 0x15, 0x38, 0x3F, 0x36, 0x31,
            0x24, 0x23, 0x2A, 0x2D, 0x70, 0x77,
            0x7E, 0x79, 0x6C, 0x6B, 0x62, 0x65,
            0x48, 0x4F, 0x46, 0x41, 0x54, 0x53,
            0x5A, 0x5D, 0xE0, 0xE7, 0xEE, 0xE9,
            0xFC, 0xFB, 0xF2, 0xF5, 0xD8, 0xDF,
            0xD6, 0xD1, 0xC4, 0xC3, 0xCA, 0xCD,
            0x90, 0x97, 0x9E, 0x99, 0x8C, 0x8B,
            0x82, 0x85, 0xA8, 0xAF, 0xA6, 0xA1,
            0xB4, 0xB3, 0xBA, 0xBD, 0xC7, 0xC0,
            0xC9, 0xCE, 0xDB, 0xDC, 0xD5, 0xD2,
            0xFF, 0xF8, 0xF1, 0xF6, 0xE3, 0xE4,
            0xED, 0xEA, 0xB7, 0xB0, 0xB9, 0xBE,
            0xAB, 0xAC, 0xA5, 0xA2, 0x8F, 0x88,
            0x81, 0x86, 0x93, 0x94, 0x9D, 0x9A,
            0x27, 0x20, 0x29, 0x2E, 0x3B, 0x3C,
            0x35, 0x32, 0x1F, 0x18, 0x11, 0x16,
            0x03, 0x04, 0x0D, 0x0A, 0x57, 0x50,
            0x59, 0x5E, 0x4B, 0x4C, 0x45, 0x42,
            0x6F, 0x68, 0x61, 0x66, 0x73, 0x74,
            0x7D, 0x7A, 0x89, 0x8E, 0x87, 0x80,
            0x95, 0x92, 0x9B, 0x9C, 0xB1, 0xB6,
            0xBF, 0xB8, 0xAD, 0xAA, 0xA3, 0xA4,
            0xF9, 0xFE, 0xF7, 0xF0, 0xE5, 0xE2,
            0xEB, 0xEC, 0xC1, 0xC6, 0xCF, 0xC8,
            0xDD, 0xDA, 0xD3, 0xD4, 0x69, 0x6E,
            0x67, 0x60, 0x75, 0x72, 0x7B, 0x7C,
            0x51, 0x56, 0x5F, 0x58, 0x4D, 0x4A,
            0x43, 0x44, 0x19, 0x1E, 0x17, 0x10,
            0x05, 0x02, 0x0B, 0x0C, 0x21, 0x26,
            0x2F, 0x28, 0x3D, 0x3A, 0x33, 0x34,
            0x4E, 0x49, 0x40, 0x47, 0x52, 0x55,
            0x5C, 0x5B, 0x76, 0x71, 0x78, 0x7F,
            0x6A, 0x6D, 0x64, 0x63, 0x3E, 0x39,
            0x30, 0x37, 0x22, 0x25, 0x2C, 0x2B,
            0x06, 0x01, 0x08, 0x0F, 0x1A, 0x1D,
            0x14, 0x13, 0xAE, 0xA9, 0xA0, 0xA7,
            0xB2, 0xB5, 0xBC, 0xBB, 0x96, 0x91,
            0x98, 0x9F, 0x8A, 0x8D, 0x84, 0x83,
            0xDE, 0xD9, 0xD0, 0xD7, 0xC2, 0xC5,
            0xCC, 0xCB, 0xE6, 0xE1, 0xE8, 0xEF,
            0xFA, 0xFD, 0xF4, 0xF3
        };

        public static readonly Dictionary<string, string> Commands = new Dictionary<string, string> {
            {"FFFF", "INVALID"},
            {"0103", "GET_SERIAL"},
            {"0105", "RESET_ESP"},
            {"0107", "NAME"},
            {"0108", "GROUP_NAME"},
            {"0109", "GROUP_NUMBER"},
            {"010A", "DEVICE_DISCOVERY"},
            {"010C", "SUBSCRIBE"},
            {"010D", "DISCOVERY_START"},
            {"010E", "DISCOVERY_STOP"},
            {"0110", "REMOTE_REFRESH"},
            {"0111", "REFRESH_CLIENTS"},
            {"0113", "UNKNOWN"},
            {"0115", "READ_BOOTLOADER_MODE"},
            {"0117", "STOP_ESP_DRIVERS"},
            {"0201", "READ_CONNECT_VERSION?"},
            {"0202", "READ_PCI_VERSION"},
            {"0203", "READ_DIAGNOSTIC"},
            {"0301", "MODE"},
            {"0302", "BRIGHTNESS"},
            {"0303", "ZONES"},
            {"0304", "ZONES_BRIGHTNESS"},
            {"0305", "AMBIENT_COLOR"},
            {"0306", "SATURATION"},
            {"0308", "AMBIENT_MODE_TYPE"},
            {"0309", "MUSIC_MODE_TYPE"},
            {"030A", "MUSIC_MODE_COLORS"},
            {"030B", "MUSIC_MODE_WEIGHTS"},
            {"030C", "MINIMUM_LUMINOSITY"},
            {"030D", "AMBIENT_SCENE"},
            {"030E", "FADE_RATE"},
            {"0313", "INDICATOR_LIGHT_AUTOOFF"},
            {"0314", "USB_POWER_ENABLE"},
            {"0316", "COLOR_DATA"},
            {"0317", "SECTOR_ASSIGNMENT"},
            {"0318", "SECTOR_BROADCAST_CONTROL"},
            {"0319", "SECTOR_BROADCAST_TIMING"},
            {"0320", "HDMI_INPUT"},
            {"0321", "MUSIC_MODE_SOURCE"},
            {"0323", "HDMI_INPUT_1_NAME"},
            {"0324", "HDMI_INPUT_2_NAME"},
            {"0325", "HDMI_INPUT_3_NAME"},
            {"0326", "CEC_PASSTHROUGH_ENABLE"},
            {"0327", "CEC_SWITCHING_ENABLE"},
            {"0328", "HDP_ENABLE"},
            {"032A", "VIDEO_FRAME_DELAY"},
            {"032B", "LETTERBOXING_ENABLE"},
            {"032C", "HDMI_ACTIVE_CHANNELS"},
            {"032D", "COLOR_BOOST"},
            {"032E", "CEC_POWER_ENABLE"},
            {"032F", "PILLARBOXING_ENABLE"},
            {"0340", "SKU_SETUP"},
            {"0341", "FLEX_SETUP"},
            {"0360", "HDR_TONE_REMAPPING"},
            {"0401", "BOOTLOADER_SETUP"},
            {"0402", "RESET_PIC"},
            {"0403", "FACTORY_RESET_DS"},
            {"040D", "ESP_CONNECTED_TO_WIFI"},
            {"0414", "OTHER_CONNECTED_TO_WIFI"},
            {"0501", "DISPLAY_ANIMATION"},
            {"0502", "AMBIENT_LIGHT_AUTO_ADJUST"},
            {"0503", "MICROPHONE_AUDIO_BROADCAST_ENABLE"},
            {"0510", "IR_ENABLE"},
            {"0511", "SET_IR_LEARNING_MODE"},
            {"0513", "SET_IR_MANIFEST_ENTRY"},
            {"0520", "SET_EMAIL_ADDRESS"},
            {"0521", "SET_THING_NAME"},
            {"0516", "COLOR_DATA_V2"}
        };

        
        public static readonly Dictionary<string, byte[]> CommandBytes = new Dictionary<string, byte[]> {
            {"name", new byte[]{0x01,0x07}},
            {"groupName", new byte[]{0x01,0x08}},
            {"groupNum", new byte[]{0x01,0x09}},
            {"mode", new byte[]{0x03,0x01}},
            {"brightness", new byte[]{0x03,0x02}},
            {"zones", new byte[]{0x03,0x03}},
            {"zonesBrightness", new byte[]{0x03,0x04}},
            {"ambientColor", new byte[]{0x03,0x05}},
            {"saturation", new byte[]{0x03,0x06}},
            {"ambientModeType", new byte[]{0x03,0x08}},
            {"musicModeType", new byte[]{0x03,0x09}},
            {"musicModeColors", new byte[]{0x03,0x0A}},
            {"musicModeWeights", new byte[]{0x03,0x0B}},
            {"minimumLuminosity", new byte[]{0x03,0x0C}},
            {"ambientScene", new byte[]{0x03,0x0D}},
            {"fadeRate", new byte[]{0x03,0x0E}},
            {"indicatorLightAutoOff", new byte[]{0x03,0x13}},
            {"usbPowerEnable", new byte[]{0x03,0x14}},
            {"colorData", new byte[]{0x03,0x16}},
            {"sectorAssignment", new byte[]{0x03,0x17}},
            {"hdmiInput", new byte[]{0x03,0x20}},
            {"musicModeSource", new byte[]{0x03,0x21}},
            {"hdmiInput1Name", new byte[]{0x03,0x23}},
            {"hdmiInput2Name", new byte[]{0x03,0x24}},
            {"hdmiInput3Name", new byte[]{0x03,0x25}},
            {"cecPassthroughEnable", new byte[]{0x03,0x26}},
            {"cecSwitchingEnable", new byte[]{0x03,0x27}},
            {"hdpEnable", new byte[]{0x03,0x28}},
            {"videoFrameDelay", new byte[]{0x03,0x2A}},
            {"letterboxingEnable", new byte[]{0x03,0x2B}},
            {"hdmiActiveChannels", new byte[]{0x03,0x2C}},
            {"colorBoost", new byte[]{0x03,0x2D}},
            {"cecPowerEnable", new byte[]{0x03,0x2E}},
            {"pillarboxingEnable", new byte[]{0x03,0x2F}},
            {"hdrToneRemapping", new byte[]{0x03,0x60}},
            {"ambientLightAutoAdjust", new byte[]{0x05,0x02}},
            {"microphoneAudioBroadcastEnable", new byte[]{0x05,0x03}},
            {"irEnable", new byte[]{0x05,0x10}}
        };


        public static byte CalculateCrc(byte[] data) {
            if (data != null) {
                var size = (byte) (data[1] + 1);
                byte crc = 0;
                for (byte counter = 0; counter < size; counter = (byte) (counter + 1))
                    crc = Crc8Table[(byte) (data[counter] ^ crc) & 255];
                return crc;
            }

            throw new ArgumentNullException(nameof(data));
        }

        public static bool CheckCrc(byte[] data) {
            if (data is null) throw new ArgumentNullException(nameof(data));

            var checkCrc = data[^1];
            data = data.Take(data.Length - 1).ToArray();
            var size = (byte) (data[1] + 1);
            byte crc = 0;
            for (byte counter = 0; counter < size; counter = (byte) (counter + 1))
                crc = Crc8Table[(byte) (data[counter] ^ crc) & 255];

            return crc == checkCrc;
        }

        public static byte GetCrc(byte[] data) {
            if (data is null) throw new ArgumentNullException(nameof(data));

            var checkCrc = data[^1];
            data = data.Take(data.Length - 1).ToArray();
            var size = (byte) (data[1] + 1);
            byte crc = 0;
            for (byte counter = 0; counter < size; counter = (byte) (counter + 1))
                crc = Crc8Table[(byte) (data[counter] ^ crc) & 255];

            return crc;
        }
    }
}