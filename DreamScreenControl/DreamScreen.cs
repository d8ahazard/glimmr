using HueDream.HueDream;
using HueDream.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HueDream.DreamScreenControl {
    class DreamScreen {

        private static byte[] crc8_table = new byte[] {
         (byte)0x00, (byte)0x07, (byte)0x0E, (byte)0x09, (byte)0x1C, (byte)0x1B,
         (byte)0x12, (byte)0x15, (byte)0x38, (byte)0x3F, (byte)0x36, (byte)0x31,
         (byte)0x24, (byte)0x23, (byte)0x2A, (byte)0x2D, (byte)0x70, (byte)0x77,
         (byte)0x7E, (byte)0x79, (byte)0x6C, (byte)0x6B, (byte)0x62, (byte)0x65,
         (byte)0x48, (byte)0x4F, (byte)0x46, (byte)0x41, (byte)0x54, (byte)0x53,
         (byte)0x5A, (byte)0x5D, (byte)0xE0, (byte)0xE7, (byte)0xEE, (byte)0xE9,
         (byte)0xFC, (byte)0xFB, (byte)0xF2, (byte)0xF5, (byte)0xD8, (byte)0xDF,
         (byte)0xD6, (byte)0xD1, (byte)0xC4, (byte)0xC3, (byte)0xCA, (byte)0xCD,
         (byte)0x90, (byte)0x97, (byte)0x9E, (byte)0x99, (byte)0x8C, (byte)0x8B,
         (byte)0x82, (byte)0x85, (byte)0xA8, (byte)0xAF, (byte)0xA6, (byte)0xA1,
         (byte)0xB4, (byte)0xB3, (byte)0xBA, (byte)0xBD, (byte)0xC7, (byte)0xC0,
         (byte)0xC9, (byte)0xCE, (byte)0xDB, (byte)0xDC, (byte)0xD5, (byte)0xD2,
         (byte)0xFF, (byte)0xF8, (byte)0xF1, (byte)0xF6, (byte)0xE3, (byte)0xE4,
         (byte)0xED, (byte)0xEA, (byte)0xB7, (byte)0xB0, (byte)0xB9, (byte)0xBE,
         (byte)0xAB, (byte)0xAC, (byte)0xA5, (byte)0xA2, (byte)0x8F, (byte)0x88,
         (byte)0x81, (byte)0x86, (byte)0x93, (byte)0x94, (byte)0x9D, (byte)0x9A,
         (byte)0x27, (byte)0x20, (byte)0x29, (byte)0x2E, (byte)0x3B, (byte)0x3C,
         (byte)0x35, (byte)0x32, (byte)0x1F, (byte)0x18, (byte)0x11, (byte)0x16,
         (byte)0x03, (byte)0x04, (byte)0x0D, (byte)0x0A, (byte)0x57, (byte)0x50,
         (byte)0x59, (byte)0x5E, (byte)0x4B, (byte)0x4C, (byte)0x45, (byte)0x42,
         (byte)0x6F, (byte)0x68, (byte)0x61, (byte)0x66, (byte)0x73, (byte)0x74,
         (byte)0x7D, (byte)0x7A, (byte)0x89, (byte)0x8E, (byte)0x87, (byte)0x80,
         (byte)0x95, (byte)0x92, (byte)0x9B, (byte)0x9C, (byte)0xB1, (byte)0xB6,
         (byte)0xBF, (byte)0xB8, (byte)0xAD, (byte)0xAA, (byte)0xA3, (byte)0xA4,
         (byte)0xF9, (byte)0xFE, (byte)0xF7, (byte)0xF0, (byte)0xE5, (byte)0xE2,
         (byte)0xEB, (byte)0xEC, (byte)0xC1, (byte)0xC6, (byte)0xCF, (byte)0xC8,
         (byte)0xDD, (byte)0xDA, (byte)0xD3, (byte)0xD4, (byte)0x69, (byte)0x6E,
         (byte)0x67, (byte)0x60, (byte)0x75, (byte)0x72, (byte)0x7B, (byte)0x7C,
         (byte)0x51, (byte)0x56, (byte)0x5F, (byte)0x58, (byte)0x4D, (byte)0x4A,
         (byte)0x43, (byte)0x44, (byte)0x19, (byte)0x1E, (byte)0x17, (byte)0x10,
         (byte)0x05, (byte)0x02, (byte)0x0B, (byte)0x0C, (byte)0x21, (byte)0x26,
         (byte)0x2F, (byte)0x28, (byte)0x3D, (byte)0x3A, (byte)0x33, (byte)0x34,
         (byte)0x4E, (byte)0x49, (byte)0x40, (byte)0x47, (byte)0x52, (byte)0x55,
         (byte)0x5C, (byte)0x5B, (byte)0x76, (byte)0x71, (byte)0x78, (byte)0x7F,
         (byte)0x6A, (byte)0x6D, (byte)0x64, (byte)0x63, (byte)0x3E, (byte)0x39,
         (byte)0x30, (byte)0x37, (byte)0x22, (byte)0x25, (byte)0x2C, (byte)0x2B,
         (byte)0x06, (byte)0x01, (byte)0x08, (byte)0x0F, (byte)0x1A, (byte)0x1D,
         (byte)0x14, (byte)0x13, (byte)0xAE, (byte)0xA9, (byte)0xA0, (byte)0xA7,
         (byte)0xB2, (byte)0xB5, (byte)0xBC, (byte)0xBB, (byte)0x96, (byte)0x91,
         (byte)0x98, (byte)0x9F, (byte)0x8A, (byte)0x8D, (byte)0x84, (byte)0x83,
         (byte)0xDE, (byte)0xD9, (byte)0xD0, (byte)0xD7, (byte)0xC2, (byte)0xC5,
         (byte)0xCC, (byte)0xCB, (byte)0xE6, (byte)0xE1, (byte)0xE8, (byte)0xEF,
         (byte)0xFA, (byte)0xFD, (byte)0xF4, (byte)0xF3
        };

       
        public IPAddress dreamScreenIp { get; set; }
        public string[][] colors { get; }
        private Socket dreamScreenSocket;
        private int Port = 8888;
        private IPEndPoint endPoint;
        private IPEndPoint receiverPort;
        private byte groupNumber = 0;
        private UdpClient receiver;
        private DreamData dd;
        public static bool listening { get; set; }
        public static bool subscribed { get; set; }

        private CancellationTokenSource cts;

        public DreamScreen() {
            dd = new DreamData();
            string dsIp = dd.DS_IP;
            dreamScreenIp = IPAddress.Parse(dsIp);
            endPoint = new IPEndPoint(dreamScreenIp, Port);
            groupNumber = 1;
            colors = new string[12][];
            for (int i = 0; i < colors.Length; i++) {
                colors[i] = new string[] { "FF", "FF", "FF" };
            }
            // Create a listening socket
            dreamScreenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            dreamScreenSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, (int)1);
        }

        public void setMode(int mode) {
            sendUDPWrite((byte)3, (byte)1, new byte[] { (byte)mode });
        }

        public void getMode() {
            requestState();
        }


        public void subscribe() {
            if (!subscribed) {
                sendUDPWrite((byte)0x01, (byte)0x0C, new byte[] { (byte)0x01 }, (byte)0x10);
                subscribed = true;
            }
        }



        public async Task Listen() {

            if (!listening) {
                // Create UDP client
                try {
                    receiverPort = new IPEndPoint(IPAddress.Any, 8888); ;
                    receiver = new UdpClient();
                    receiver.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    receiver.EnableBroadcast = true;
                    receiver.Client.Bind(receiverPort);
                    listening = true;
                } catch (SocketException e) {
                    Console.WriteLine("Socket exception: " + e.Message);
                }
                Console.WriteLine("Starting DreamScreen Upd receiving on port: " + receiverPort);
                // Call DataReceived() every time it gets something
                receiver.BeginReceive(DataReceived, receiver);
            } else {
                Console.WriteLine("DreamScreen: Listen terminated");
            }
        }




        private void DataReceived(IAsyncResult ar) {
            UdpClient c = (UdpClient)ar.AsyncState;
            IPEndPoint receivedIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
            byte[] receivedBytes = c.EndReceive(ar, ref receivedIpEndPoint);
            // Convert data to ASCII and print in console
            if (!CheckCrc(receivedBytes)) {
                Console.WriteLine("CRC Check Failed!");
                return;
            }
            string receivedText = Encoding.ASCII.GetString(receivedBytes);
            string byteString = BitConverter.ToString(receivedBytes);
            string[] bytesString = byteString.Split("-");
            string command = null;
            string flag = null;
            string from = receivedIpEndPoint.ToString();
            string[] payload = Array.Empty<string>();
            try {
                DreamScreenMessage msg = new DreamScreenMessage(byteString);
                payload = msg.payload;
                command = msg.command;
                if (command != "SUBSCRIBE") Console.WriteLine(from + " -> " + JsonConvert.SerializeObject(msg));

                flag = msg.flags;
            } catch (Exception e) {
                Console.WriteLine("Exception: " + e.Message);
            }

            switch (command) {
                case "SUBSCRIBE":
                    if (subscribed) {
                        sendUDPWrite((byte)0x01, (byte)0x0C, new byte[] { (byte)0x01 }, (byte)0x10); // Send sub response
                    }
                    break;
                case "COLORDATA":
                    if (subscribed) {
                        IEnumerable<string> colorData = bytesString.Skip(6); // Swap this with payload
                        int lightCount = 0;
                        int count = 0;
                        string[] colorList = new string[3];
                        foreach (string colorValue in colorData) {
                            if (count == 0) {
                                colorList = new string[3];
                            }
                            colorList[count] = colorValue;
                            if (count == 2) {
                                colors[lightCount] = colorList;
                                lightCount++;
                                if (lightCount > 11) break;
                                count = 0;
                            } else {
                                count++;
                            }
                        }
                    }
                    break;
                case "DEVICE_DISCOVERY":
                    if (flag == "30") {
                        IPEndPoint target = receivedIpEndPoint;
                        target.Port = 8888;
                        SendDeviceStatus(target);
                    }
                    break;
                case "GROUP_NAME":

                    string gName = ByteStringUtil.HexString(string.Join("", payload));
                    dd.DS_GROUP_NAME = gName;
                    dd.saveData();
                    break;
                case "GROUP_NUMBER":
                    int gNum = ByteStringUtil.HexInt(payload[0]);
                    dd.DS_GROUP_ID = gNum;
                    dd.saveData();
                    break;
                case "NAME":
                    string dName = ByteStringUtil.HexString(string.Join("", payload));
                    dd.DS_DEVICE_NAME = dName;
                    dd.saveData();
                    break;
                case "BRIGHTNESS":
                    dd.DS_BRIGHTNESS = ByteStringUtil.HexInt(payload[0]);
                    dd.saveData();
                    break;
                case "SATURATION":
                    dd.DS_SATURATION = string.Join("", payload);
                    dd.saveData();
                    break;
                case "MODE":
                    dd.DS_MODE = ByteStringUtil.HexInt(payload[0]);
                    dd.saveData();
                    break;
                case "AMBIENT_MODE_TYPE":
                    dd.DS_AMBIENT_MODE_TYPE = ByteStringUtil.HexInt(payload[0]);
                    dd.saveData();
                    break;
                case "AMBIENT_COLOR":
                    dd.DS_AMBIENT_COLOR = string.Join("", payload);
                    dd.saveData();
                    break;
            }

            // Restart listening for udp data packages
            if (listening) {
                c.BeginReceive(DataReceived, ar.AsyncState);
            } else {
                Console.WriteLine("DreamScreen: Closing listening loop.");
                c.Close();
            }
        }

        public void SendDeviceStatus(IPEndPoint src) {
            DreamScreenState dss = new DreamScreenState();
            dss.name = dd.DS_DEVICE_NAME;
            dss.groupNumber = dd.DS_GROUP_ID;
            dss.groupName = dd.DS_GROUP_NAME; // Change this to match main unit, I think.
            dss.mode = dd.HUE_SYNC ? 1 : 0;
            dss.toneRemapping = 0;
            dss.type = "Connect";
            dss.brightness = dd.DS_BRIGHTNESS;
            dss.color = dd.DS_AMBIENT_COLOR;
            dss.scene = dd.DS_AMBIENT_SCENE;
            dss.saturation = dd.DS_SATURATION;
            byte[] payload = dss.EncodeState();
            this.groupNumber = ByteStringUtil.IntByte(dd.DS_GROUP_ID);
            sendUDPWrite((byte)0x01, (byte)0x0A, payload, 0x60, src);
        }


        public List<string> findDevices() {
            List<string> devices = new List<string>();
            byte[] payload = new byte[] { 0x01 };
            sendUDPWrite(0x01, 0x0A, payload, 0x30);
            return devices;
        }

        public static string GetLocalIPAddress() {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList) {
                if (ip.AddressFamily == AddressFamily.InterNetwork) {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }


        void sendUDPWrite(byte command1, byte command2, byte[] payload, byte flag = (byte)17, IPEndPoint ep = null) {
            if (ep == null) ep = endPoint;
            using (MemoryStream stream = new MemoryStream()) {
                using (BinaryWriter response = new BinaryWriter(stream)) {
                    // Magic header
                    response.Write((byte)0xFC);
                    // Payload length
                    response.Write((byte)(payload.Length + 5));
                    // Group number
                    response.Write((byte)this.groupNumber);
                    // Flag, should be 0x10 for subscription, 17 for everything else
                    response.Write(flag);
                    // Upper command
                    response.Write(command1);
                    // Lower command
                    response.Write(command2);
                    // Payload
                    foreach (byte b in payload) {
                        response.Write(b);
                    }

                    var byteSend = stream.ToArray();
                    // CRC
                    response.Write(CalculateCrc(byteSend));
                    string msg = BitConverter.ToString(stream.ToArray());
                    SendUDPUnicast(stream.ToArray(), ep);

                }
            }
        }

        void requestState() {

            using (MemoryStream stream = new MemoryStream()) {
                using (BinaryWriter response = new BinaryWriter(stream)) {
                    sendUDPWrite(0x01, 0x0A, Array.Empty<byte>(), 0x30);
                }
            }
        }

        private byte CalculateCrc(byte[] data) {
            byte size = (byte)(data[1] + 1);
            byte crc = 0;
            for (byte cntr = 0; cntr < size; cntr = (byte)(cntr + 1)) {
                crc = crc8_table[((byte)(data[cntr] ^ crc)) & 255];
            }
            return crc;
        }

        private bool CheckCrc(byte[] data) {
            byte checkCrc = data[data.Length - 1];
            data = data.Take(data.Length - 1).ToArray();
            byte size = (byte)(data[1] + 1);
            byte crc = 0;
            for (byte cntr = 0; cntr < size; cntr = (byte)(cntr + 1)) {
                crc = crc8_table[((byte)(data[cntr] ^ crc)) & 255];
            }

            return (crc == checkCrc);

        }

        private void SendUDPUnicast(byte[] data, IPEndPoint ep) {
            string byteString = BitConverter.ToString(data);
            string[] bytesString = byteString.Split("-");
            DreamScreenMessage sm = new DreamScreenMessage(byteString);
            Console.WriteLine("localhost:8888 -> " + ep.ToString() + " " + JsonConvert.SerializeObject(sm));
            dreamScreenSocket.SendTo(data, ep);
        }



        [Serializable]
        class DreamScreenMessage {
            public string command { get; set; }
            public string addr { get; }
            public string flags { get; }
            public string upper { get; }
            public string lower { get; }
            public string[] payload { get; }
            public string hex { get; }

            public DreamScreenState state { get; set; }

            private static Dictionary<string, string> commands;

            public DreamScreenMessage(string byteString) {
                commands = new Dictionary<string, string> {
                    { "FFFF", "INVALID" },
                    { "0105", "RESET_ESP" },
                    { "0107", "NAME" },
                    { "0108", "GROUP_NAME" },
                    { "0109", "GROUP_NUMBER" },
                    { "010A", "DEVICE_DISCOVERY" },
                    { "010C", "SUBSCRIBE" },
                    { "0113", "UNKNOWN" },
                    { "0115", "READ_BOOTLOADER_MODE" },
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
                    { "0313", "INDICATOR_LIGHT_AUTOOFF" },
                    { "0314", "USB_POWER_ENABLE" },
                    { "0316", "COLOR_DATA" },
                    { "0317", "SECTOR_ASSIGNMENT" },
                    { "0318", "SECTOR_BROADCAST_CONTROL" },
                    { "0319", "SECTOR_BROADCAST_TIMING" },
                    { "0320", "HDMI_INPUT" }, // Define channels 01, 02, 03
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
                    { "032E", "CEC_POWER_ENABLE" },
                    { "032F", "PILLARBOXING_ENABLE" },
                    { "0340", "SKU_SETUP" },
                    { "0341", "FLEX_SETUP" },
                    { "0360", "HDR_TONE_REMAPPING" },
                    { "0401", "BOOTLOADER_SETUP" },
                    { "0402", "RESET_PIC" },
                    { "040D", "ESP_CONNECTED_TO_WIFI" },
                    { "0414", "OTHER_CONNECTED_TO_WIFI" },
                    { "0510", "IR_ENABLE" },
                    { "0511", "ADD_IR_CODE" }
                };
                string[] bytesIn = byteString.Split("-");
                hex = string.Join("", bytesIn);
                string magic = bytesIn[0];
                int len = int.Parse(bytesIn[1], System.Globalization.NumberStyles.HexNumber);
                addr = bytesIn[2];
                flags = bytesIn[3];
                upper = bytesIn[4];
                lower = bytesIn[5];
                string cmd = bytesIn[4] + bytesIn[5];
                if (commands.ContainsKey(cmd)) {
                    command = commands[cmd];
                } else {
                    Console.WriteLine("No matching key in dict for bytes: " + cmd);
                }

                if (magic == "FC") {
                    if (len > 5) {
                        payload = Payload(bytesIn);
                    }
                    if (command == "DEVICE_DISCOVERY" && flags == "60") {
                        state = new DreamScreenState();
                        state.LoadState(payload);
                        payload = null;
                    }
                } else {
                    Console.WriteLine("Error, magic missing.");
                }
            }

            static string[] Payload(string[] source) {
                int i = 0;
                List<string> output = new List<string>();
                foreach (string s in source) {
                    if (i > 5 && i < source.Length - 1) {
                        output.Add(s);
                    }
                    i++;
                }
                return output.ToArray();
            }


        }

        /// <summary>
        /// The state of a dreamscreen device.
        /// </summary>
        [Serializable]
        class DreamScreenState {
            public string type { get; set; }
            public int state { get; set; }
            public string name { get; set; }
            public string groupName { get; set; }
            public int groupNumber { get; set; }
            public int mode { get; set; }
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


            public DreamScreenState() {

            }

            /// <summary>
            /// Load a device state message
            /// </summary>
            /// <param name="stateMessage"></param>
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
                }


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
                byte[] nByte = ByteStringUtil.StringBytePad(name, 16);
                response.AddRange(nByte);
                // Write padded group
                byte[] gByte = ByteStringUtil.StringBytePad(groupName, 16);
                response.AddRange(gByte);
                // Group number
                response.Add(ByteStringUtil.IntByte(groupNumber));
                // Mode 
                response.Add(ByteStringUtil.IntByte(mode));
                // Brightness
                response.Add(ByteStringUtil.IntByte(brightness));
                int i = 0;
                if (type == "SideKick") {
                    // Ambient color (3byte)
                    string cString = "";
                    // Ambient color (3byte)
                    response.AddRange(ByteStringUtil.HexBytes(color));
                    // Saturation color (3byte)
                    response.AddRange(ByteStringUtil.HexBytes(saturation));
                    // Pad 1??
                    response.Add(0x00);
                    // Sector data?
                    byte[] bAdd = { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0b, 0x0C };
                    response.AddRange(bAdd);

                    // Pad 6 bytes, scene needs to be at 60
                    response.AddRange(new byte[6]);

                    // Scene
                    response.Add(ByteStringUtil.IntByte(scene));
                    response.Add(0x00);
                    // Type
                    response.Add(0x03);

                } else if (type == "Connect") {
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
                    // Flag 0x1? Wifi set??
                    response.Add(0x00);
                    // IR Enabled
                    response.Add(0x01);
                    // Pad 1
                    response.Add(0x00);
                    // Pad 40 - This is the bank of IR codes, with 8 slots of 5, value 0 is the action ID, the other 4 are for the code
                    response.AddRange(new byte[40]);
                    // Pad 7. Some key?
                    response.AddRange(new byte[7]);
                    // Pad 38 - this might be a google auth token
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
                    response.AddRange(ByteStringUtil.HexBytes(color));
                    // Pad 20
                    response.AddRange(new byte[20]);
                    // Ambient scene (@byte 62)
                    response.Add(ByteStringUtil.IntByte(scene));
                    // Pad 11
                    response.AddRange(new byte[11]);
                    // HDMI Input (@byte 73)
                    response.Add(ByteStringUtil.IntByte(input));
                    // Pad 2
                    response.AddRange(new byte[2]);                    

                    // HDMI Interface names
                    string[] iList = { inputName0, inputName1, inputName2 };
                    foreach (string iName in iList) {
                        response.AddRange(ByteStringUtil.StringBytePad(iName, 16));                        
                    }

                    // Pad 7
                    response.AddRange(new byte[7]);

                    // HDMI Active Channels
                    response.Add(ByteStringUtil.IntByte(activeChannels));

                    // Pad 10
                    response.AddRange(new byte[10]);

                    response.Add(ByteStringUtil.IntByte(toneRemapping));

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


}
