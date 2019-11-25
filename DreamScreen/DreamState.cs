using HueDream.Util;
using System;
using System.Collections.Generic;

namespace HueDream.DreamScreen {
    /// <summary>
    /// The state of a dreamscreen device.
    /// </summary>
    [Serializable]
    public class DreamState {
        public string type { get; set; }
        public int state { get; set; }
        public string name { get; set; }
        public string groupName { get; set; }
        public string ipAddress { get; set; }
        public int groupNumber { get; set; }
        public int mode { get; set; }
        public int ambientMode { get; set; }
        public int brightness { get; set; }
        public string color { get; set; }
        public string saturation { get; set; }
        public int scene { get; set; }
        public int input { get; set; }
        public string inputName0 { get; set; }
        public string inputName1 { get; set; }
        public string inputName2 { get; set; }
        public int activeChannels { get; set; }
        public int toneRemapping { get; set; }

        /// <summary>
        /// Load a device state message
        /// </summary>
        /// <param name="stateMessage"></param>
        /// 
        public void SetDefaults() {
            type = "SideKick";
            name = "HueDream";
            groupName = "undefined";
            groupNumber = 100;
            mode = 0;
            color = "FFFFFF";
            saturation = "FFFFFF";
            scene = 0;
        }

        public void LoadState(string[] stateMessage) {
            switch (stateMessage[stateMessage.Length - 1]) {
                case "01":
                    type = "DreamScreen";
                    break;
                case "02":
                    type = "DreamScreen 4K";
                    break;
                case "03":
                    type = "SideKick";
                    break;
                case "04":
                    type = "Connect";
                    break;
                case "07":
                    type = "DreamScreen Solo";
                    break;
            }

            Console.WriteLine("Parsing DS State message: " + string.Join("", stateMessage));
            if (!string.IsNullOrEmpty(type)) {
                name = ByteStringUtil.ExtractHexString(stateMessage, 0, 16);
                groupName = ByteStringUtil.ExtractHexString(stateMessage, 16, 16);
                groupNumber = ByteStringUtil.HexInt(stateMessage[32]);
                mode = ByteStringUtil.HexInt(stateMessage[33]);
                brightness = ByteStringUtil.HexInt(stateMessage[34]);
            }

            if (type == "SideKick") {
                color = stateMessage[35] + stateMessage[36] + stateMessage[37];
                saturation = stateMessage[38] + stateMessage[39] + stateMessage[40];
                scene = ByteStringUtil.HexInt(stateMessage[60]);
            } else if (type == "Connect") {
                color = stateMessage[35] + stateMessage[36] + stateMessage[37];
                scene = ByteStringUtil.HexInt(stateMessage[60]);
            } else {
                color = stateMessage[40] + stateMessage[41] + stateMessage[42];
                scene = ByteStringUtil.HexInt(stateMessage[62]);
                input = ByteStringUtil.HexInt(stateMessage[73]);
                inputName0 = ByteStringUtil.ExtractHexString(stateMessage, 75, 16);
                inputName1 = ByteStringUtil.ExtractHexString(stateMessage, 91, 16);
                inputName2 = ByteStringUtil.ExtractHexString(stateMessage, 107, 16);
                activeChannels = ByteStringUtil.HexInt(stateMessage[129]);
                toneRemapping = ByteStringUtil.HexInt(stateMessage[139]);
            }
        }

        /// <summary>
        /// Encode our state into a DreamScreen payload
        /// </summary>
        /// <returns>A byte array</returns>
        public byte[] EncodeState() {
            List<byte> response = new List<byte>();
            // Write padded Device name
            Console.WriteLine("Padname");
            byte[] nByte = ByteStringUtil.StringBytePad(name, 16);
            response.AddRange(nByte);
            // Write padded group
            Console.WriteLine("PadGname");
            byte[] gByte = ByteStringUtil.StringBytePad(groupName, 16);
            response.AddRange(gByte);
            // Group number
            Console.WriteLine("gnum");
            response.Add(ByteStringUtil.IntByte(groupNumber));
            // Mode 
            Console.WriteLine("mode");
            response.Add(ByteStringUtil.IntByte(mode));
            // Brightness
            Console.WriteLine("bright");
            response.Add(ByteStringUtil.IntByte(brightness));
            int i = 0;
            if (type == "SideKick") {
                Console.WriteLine("skmsg - col");
                // Ambient color (3byte)
                string cString = "";
                // Ambient color (3byte)
                response.AddRange(ByteStringUtil.HexBytes(color));
                // Saturation color (3byte)
                Console.WriteLine("sat");
                response.AddRange(ByteStringUtil.HexBytes(saturation));
                // Pad 1??
                Console.WriteLine("Pad");
                response.Add(0x00);
                // Sector data?
                byte[] bAdd = { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0b, 0x0C };
                response.AddRange(bAdd);

                // Pad 6 bytes, scene needs to be at 60
                response.AddRange(new byte[6]);

                // Scene
                Console.WriteLine("Scene");
                response.Add(ByteStringUtil.IntByte(scene));
                response.Add(0x00);
                // Type
                Console.WriteLine("Type");
                response.Add(0x03);

            } else if (type == "Connect") {
                Console.WriteLine("Cmsg - col");
                // color (3byte)
                response.AddRange(ByteStringUtil.HexBytes(color));
                // Pad 3, probably for unused saturation
                response.AddRange(new byte[3]);
                // I don't know what this does yet, but it's either 0x32 or 0x04, so let's use 4
                response.Add(0x04);
                // Pad 16, Not sure what this is for
                response.AddRange(new byte[16]);
                // Always 4? 
                response.Add(0x04);
                // Hue linked? Not sure
                response.Add(0x01);
                // Ambient scene
                response.Add(0x00);
                // Pad 3
                response.AddRange(new byte[3]);
                // Flag 0x1? Doesn't seem to do anything
                response.Add(0x00);
                // IR Enabled
                response.Add(0x01);
                // Pad 1
                response.Add(0x00);
                // Pad 40 - This is the bank of IR codes, with 8 slots of 5, value 0 is the action ID, the other 4 are for the code
                response.AddRange(new byte[40]);
                // Pad 7. Some key?
                response.AddRange(new byte[7]);
                // Pad 38 - this might be a google auth token, hue token is 32 hex
                response.AddRange(new byte[38]);
                // Pad 25 - Hue token? Not sure yet
                response.AddRange(new byte[25]);
                // Finally, our device type!
                response.Add(0x04);
            } else {
                // Dreamscreen/Dreamscreen 4k state
                // Pad 6 before adding ambient
                response.AddRange(new byte[6]);
                // Ambient color (3byte)
                Console.WriteLine("DS MSG - color");
                response.AddRange(ByteStringUtil.HexBytes(color));
                // Pad 20
                response.AddRange(new byte[20]);
                Console.WriteLine("DS MSG - scene");
                // Ambient scene (@byte 62)
                response.Add(ByteStringUtil.IntByte(scene));
                // Pad 11
                response.AddRange(new byte[11]);
                // HDMI Input (@byte 73)
                Console.WriteLine("input");
                response.Add(ByteStringUtil.IntByte(input));
                // Pad 2
                response.AddRange(new byte[2]);
                Console.WriteLine("hdmiInterfaces");
                // HDMI Interface names
                string[] iList = { inputName0, inputName1, inputName2 };
                foreach (string iName in iList) {
                    response.AddRange(ByteStringUtil.StringBytePad(iName, 16));
                }

                // Pad 7
                response.AddRange(new byte[7]);
                Console.WriteLine("Channels");
                // HDMI Active Channels
                response.Add(ByteStringUtil.IntByte(activeChannels));

                // Pad 10
                response.AddRange(new byte[10]);
                Console.WriteLine("Toneremapping");
                response.Add(ByteStringUtil.IntByte(toneRemapping));

                Console.WriteLine("Type");
                // Device type
                if (type == "DreamScreen") {
                    response.Add(0x01);
                } else {
                    response.Add(0x02);
                }

            }

            return response.ToArray();

        }


    }


}
