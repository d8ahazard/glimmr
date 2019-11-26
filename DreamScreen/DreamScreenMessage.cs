using HueDream.DreamScreen.Devices;
using System;
using System.Collections.Generic;

namespace HueDream.DreamScreen {

    [Serializable]
    public class DreamScreenMessage {
        public string command { get; set; }
        public string addr { get; }
        public string flags { get; }
        public string upper { get; }
        public string lower { get; }
        public byte[] payload { get; }
        public string[] payloadString { get; set; }
        public string hex { get; }

        public string ipAddress;

        private static readonly Dictionary<string, string> commands = new Dictionary<string, string> {
                    { "FFFF", "INVALID" },
                    { "0103", "GET_SERIAL" },
                    { "0105", "RESET_ESP" },
                    { "0107", "NAME" },
                    { "0108", "GROUP_NAME" },
                    { "0109", "GROUP_NUMBER" },
                    { "010A", "DEVICE_DISCOVERY" },
                    { "010C", "SUBSCRIBE" },
                    { "0113", "UNKNOWN" },
                    { "0115", "READ_BOOTLOADER_MODE" },
                    { "0117", "STOP_ESP_DRIVERS" },
                    { "0201", "READ_CONNECT_VERSION?" },
                    { "0202", "READ_PCI_VERSION" },
                    { "0203", "READ_DIAGNOSTIC" },
                    { "0301", "MODE" },
                    { "0302", "BRIGHTNESS" },
                    { "0303", "ZONES" },
                    { "0304", "ZONES_BRIGHTNESS" },
                    { "0305", "AMBIENT_COLOR" },
                    { "0306", "SATURATION" },
                    { "0308", "AMBIENT_MODE_TYPE" },
                    { "0309", "MUSIC_MODE_TYPE" },
                    { "030A", "MUSIC_MODE_COLORS" },
                    { "030B", "MUSIC_MODE_WEIGHTS" },
                    { "030C", "MINIMUM_LUMINOSITY" },
                    { "030D", "AMBIENT_SCENE" },
                    { "030E", "FADE_RATE" },
                    { "0313", "INDICATOR_LIGHT_AUTOOFF" },
                    { "0314", "USB_POWER_ENABLE" },
                    { "0316", "COLOR_DATA" },
                    { "0317", "SECTOR_ASSIGNMENT" },
                    { "0318", "SECTOR_BROADCAST_CONTROL" },
                    { "0319", "SECTOR_BROADCAST_TIMING" },
                    { "0320", "HDMI_INPUT" },
                    { "0321", "MUSIC_MODE_SOURCE" },
                    { "0323", "HDMI_INPUT_1_NAME" },
                    { "0324", "HDMI_INPUT_2_NAME" },
                    { "0325", "HDMI_INPUT_3_NAME" },
                    { "0326", "CEC_PASSTHROUGH_ENABLE" },
                    { "0327", "CEC_SWITCHING_ENABLE" },
                    { "0328", "HDP_ENABLE" },
                    { "032A", "VIDEO_FRAME_DELAY" },
                    { "032B", "LETTERBOXING_ENABLE" },
                    { "032C", "HDMI_ACTIVE_CHANNELS" },
                    { "032D", "COLOR_BOOST" },
                    { "032E", "CEC_POWER_ENABLE" },
                    { "032F", "PILLARBOXING_ENABLE" },
                    { "0340", "SKU_SETUP" },
                    { "0341", "FLEX_SETUP" },
                    { "0360", "HDR_TONE_REMAPPING" },
                    { "0401", "BOOTLOADER_SETUP" },
                    { "0402", "RESET_PIC" },
                    { "0403", "FACTORY_RESET_DS" },
                    { "040D", "ESP_CONNECTED_TO_WIFI" },
                    { "0414", "OTHER_CONNECTED_TO_WIFI" },
                    { "0501", "DISPLAY_ANIMATION" },
                    { "0502", "AMBIENT_LIGHT_AUTO_ADJUST" },
                    { "0503", "MICROPHONE_AUDIO_BROADCAST_ENABLE" },
                    { "0510", "IR_ENABLE" },
                    { "0511", "SET_IR_LEARNING_MODE" },
                    { "0513", "SET_IR_MANIFEST_ENTRY" },
                    { "0520", "SET_EMAIL_ADDRESS" },
                    { "0521", "SET_THING_NAME" },

                };

        public BaseDevice device { get; set; }


        public DreamScreenMessage(byte[] bytesIn, string from) {
            string byteString = BitConverter.ToString(bytesIn);
            string[] bytesString = byteString.Split("-");
            hex = string.Join("", bytesIn);
            string magic = bytesString[0];
            int len = bytesIn[1];
            addr = bytesString[2];
            flags = bytesString[3];
            upper = bytesString[4];
            lower = bytesString[5];
            string cmd = bytesString[4] + bytesString[5];
            if (commands.ContainsKey(cmd)) {
                command = commands[cmd];
            } else {
                Console.WriteLine("No matching key in dict for bytes: " + cmd);
            }

            BaseDevice dreamDev = null;
            if (magic == "FC") {
                if (len > 5) {
                    payload = Payload(bytesIn);
                    string pString = BitConverter.ToString(payload);
                    payloadString = pString.Split("-");
                }
                if (command == "DEVICE_DISCOVERY" && flags == "60" && len > 46) {
                    string typeByte = payloadString[payloadString.Length - 1];
                    Console.WriteLine("In Byte: " + typeByte);
                    switch (typeByte) {
                        case "01":
                            dreamDev = new Devices.DreamScreen(from);
                            break;
                        case "02":
                            dreamDev = new DreamScreen4K(from);
                            break;
                        case "03":
                            dreamDev = new SideKick(from);
                            break;
                        case "04":
                            dreamDev = new Connect(from);
                            break;
                        case "07":
                            dreamDev = new DreamScreenSolo(from);
                            break;
                    }
                    if (dreamDev != null) {
                        Console.WriteLine("Parsing payload?");
                        dreamDev.Initialize();
                        dreamDev.ParsePayload(payload);
                    } else {
                        Console.WriteLine("DreamDev is null.");
                    }
                    device = dreamDev;
                    payload = null;
                }
            } else {
                Console.WriteLine("Error, magic missing.");
            }
        }

        private static byte[] Payload(byte[] source) {
            int i = 0;
            List<byte> output = new List<byte>();
            foreach (byte b in source) {
                if (i > 5 && i < source.Length - 1) {
                    output.Add(b);
                }
                i++;
            }
            return output.ToArray();
        }


    }
}
