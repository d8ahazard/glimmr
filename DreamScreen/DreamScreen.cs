using HueDream.HueDream;
using HueDream.Util;
using Microsoft.EntityFrameworkCore.Internal;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace HueDream.DreamScreen {
    class DreamScreen : IDisposable {

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

        public string[] colors { get; }
        public static bool listening { get; set; }
        public static bool subscribed { get; set; }

        private static int Port = 8888;
        private static bool searching = false;
        public static List<DreamState> devices { get; set; }
        public int deviceMode { get; set; }
        private int groupNumber = 0;

        private DataObj dd;
        private DreamState dss;
        private DreamSync dreamSync;
        public IPAddress dreamScreenIp { get; set; }
        private IPEndPoint streamEndPoint;
        private IPEndPoint receiverPort;
        private UdpClient receiver;
        private Socket sender;


        public DreamScreen(DreamSync ds, DataObj dreamData) {
            dd = dreamData;
            devices = new List<DreamState>();
            receiver = new UdpClient();
            receiver.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            receiver.EnableBroadcast = true;
            dss = dreamData.DreamState;
            string dsIp = dd.DsIp;
            dreamSync = ds;
            Console.WriteLine("Still alive");
            dreamScreenIp = IPAddress.Parse(dsIp);
            streamEndPoint = new IPEndPoint(dreamScreenIp, Port);
            deviceMode = dss.mode;
            groupNumber = dss.groupNumber;
            colors = new string[12];
            for (int i = 0; i < colors.Length; i++) {
                colors[i] = "FFFFFF";
            }
            // Create a listening socket
            sender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            sender.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, (int)1);
        }

        public void getMode() {
            requestState();
        }

        private void updateMode(int newMode) {
            if (deviceMode != newMode) {
                deviceMode = newMode;
                dss.mode = newMode;
                dd.DreamState = dss;
                DreamData.SaveJson(dd);
                Console.WriteLine("Updating mode to " + newMode.ToString());
                // If ambient, set to ambient colorS   
                bool sync = (newMode != 0);
                dreamSync.CheckSync(sync);
                if (newMode == 3) {
                    for (int i = 0; i < colors.Length; i++) {
                        colors[i] = dd.DreamState.color;
                    }
                    Console.WriteLine("Updating color array to use ambient color: " + colors[0]);
                }
            }
        }


        public void subscribe() {
            Console.WriteLine("Subscribing to color data...");
            sendUDPWrite((byte)0x01, (byte)0x0C, new byte[] { (byte)0x01 }, (byte)0x10);
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
            DreamState dss = null;
            string from = receivedIpEndPoint.Address.ToString();
            string[] payload = Array.Empty<string>();
            try {
                DreamScreenMessage msg = new DreamScreenMessage(byteString);
                payload = msg.payload;
                command = msg.command;
                if (msg.state != null) msg.state.ipAddress = from;
                dss = msg.state;
                string[] ignore = { "SUBSCRIBE", "READ_CONNECT_VERSION?", "COLOR_DATA" };
                if (!ignore.Contains(command)) Console.WriteLine(from + " -> " + JsonConvert.SerializeObject(msg));

                flag = msg.flags;
            } catch (Exception e) {
                Console.WriteLine("MSG parse Exception: " + e.Message);
            }

            switch (command) {
                case "SUBSCRIBE":
                    if (deviceMode != 0) {
                        sendUDPWrite((byte)0x01, (byte)0x0C, new byte[] { (byte)0x01 }, (byte)0x10); // Send sub response
                    }
                    break;
                case "COLOR_DATA":
                    if (deviceMode != 0) {
                        //Console.WriteLine("ColorData: " + payload);
                        IEnumerable<string> colorData = ByteStringUtil.SplitHex(string.Join("", payload), 6); // Swap this with payload
                        int lightCount = 0;
                        int count = 0;
                        //Console.WriteLine("ColorData: " + JsonConvert.SerializeObject(colorData));
                        foreach (string colorValue in colorData) {
                            colors[lightCount] = colorValue;
                            lightCount++;
                            if (lightCount > 11) break;
                        }
                    }
                    break;
                case "DEVICE_DISCOVERY":
                    IPEndPoint target = receivedIpEndPoint;
                    if (flag == "30") {
                        target.Port = 8888;
                        SendDeviceStatus(target);
                    } else if (flag == "60") {
                        if (dss != null) {
                            Console.WriteLine("Adding devices");
                            if (dd.DsIp == "0.0.0.0" && dss.type.Contains("DreamScreen")) dd.DsIp = from;
                            DreamData.SaveJson(dd);
                            if (searching) {
                                Console.WriteLine("Adding devices for real");
                                devices.Add(dss);
                                devices.Add(dss);
                            }
                        }
                    }
                    break;
                case "GROUP_NAME":
                    string gName = ByteStringUtil.HexString(string.Join("", payload));
                    dd.DreamState.groupName = gName;
                    DreamData.SaveJson(dd);
                    break;
                case "GROUP_NUMBER":
                    int gNum = ByteStringUtil.HexInt(payload[0]);
                    dd.DreamState.groupNumber = gNum;
                    DreamData.SaveJson(dd);
                    break;
                case "NAME":
                    string dName = ByteStringUtil.HexString(string.Join("", payload));
                    dd.DreamState.name = dName;
                    DreamData.SaveJson(dd);
                    break;
                case "BRIGHTNESS":
                    dd.DreamState.brightness = ByteStringUtil.HexInt(payload[0]);
                    DreamData.SaveJson(dd);
                    break;
                case "SATURATION":
                    dd.DreamState.saturation = string.Join("", payload);
                    DreamData.SaveJson(dd);
                    break;
                case "MODE":
                    updateMode(ByteStringUtil.HexInt(payload[0]));
                    break;
                case "AMBIENT_MODE_TYPE":
                    dd.DreamState.ambientMode = ByteStringUtil.HexInt(payload[0]);
                    DreamData.SaveJson(dd);
                    break;
                case "AMBIENT_COLOR":
                    dd.DreamState.color = string.Join("", payload);
                    DreamData.SaveJson(dd);
                    for (int i = 0; i < colors.Length; i++) {
                        colors[i] = dd.DreamState.color;
                    }
                    Console.WriteLine("Updating color array to use ambient color: " + dd.DreamState.color);

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
            byte[] payload = dss.EncodeState();
            this.groupNumber = ByteStringUtil.IntByte(dd.DreamState.groupNumber);
            sendUDPWrite((byte)0x01, (byte)0x0A, payload, 0x60, src);
        }


        public async Task<List<DreamState>> findDevices() {
            searching = true;
            devices = new List<DreamState>();
            byte[] payload = new byte[] { 0xFF };
            Console.WriteLine("Sending da multicast");
            // FC:05:FF:30:01:0A:2A
            // FC:05:FF:30:01:0A:2A
            byte[] msg = new byte[] { 0xFC, 0x05, 0xFF, 0x30, 0x01, 0x0A, 0x2A };
            SendUDPBroadcast(msg);
            Stopwatch s = new Stopwatch();
            s.Start();
            Console.WriteLine("Looping");
            while (s.Elapsed < TimeSpan.FromSeconds(10)) {

            }
            devices = devices.Distinct<DreamState>().ToList<DreamState>();
            Console.WriteLine("Found Devices: " + JsonConvert.SerializeObject(devices));
            s.Stop();
            searching = false; 

            return devices;
        }


        void sendUDPWrite(byte command1, byte command2, byte[] payload, byte flag = (byte)17, IPEndPoint ep = null) {
            if (ep == null) ep = streamEndPoint;
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
                    if (flag == 0x30) {
                        SendUDPBroadcast(stream.ToArray());
                    } else {
                        SendUDPUnicast(stream.ToArray(), ep);
                    }

                }
            }
        }

        byte[] buildPacket(byte command1, byte command2, byte[] payload, byte flag = (byte)17, IPEndPoint ep = null) {
            if (ep == null) ep = streamEndPoint;
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
                    return stream.ToArray();

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
            DreamScreenMessage sm = new DreamScreenMessage(byteString);
            Console.WriteLine("localhost:8888 -> " + ep.ToString() + " " + JsonConvert.SerializeObject(sm));
            sender.EnableBroadcast = false;
            sender.SendTo(data, ep);
        }


        

        public void SendUDPBroadcast(byte[] bytes) {
            UdpClient client = new UdpClient();
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            IPEndPoint ip = new IPEndPoint(IPAddress.Parse("255.255.255.255"), 8888);
            client.Send(bytes, bytes.Length, ip);
            client.Close();
            Console.WriteLine("SENT");
        }


        public void Dispose() {
            throw new NotImplementedException();
        }
    }
}
