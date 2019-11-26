using HueDream.DreamScreen.Devices;
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
using System.Threading.Tasks;

namespace HueDream.DreamScreen {
    internal class DreamClient : IDisposable {

        private static readonly byte[] crc8_table = new byte[] {
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

        public string[] colors { get; }
        public static bool listening { get; set; }
        public static bool subscribed { get; set; }

        private static readonly int Port = 8888;
        private static bool searching = false;
        public static List<BaseDevice> devices { get; set; }
        public int deviceMode { get; set; }
        private int groupNumber = 0;

        private readonly DataObj dd;
        private readonly BaseDevice dss;
        private readonly DreamSync dreamSync;
        public IPAddress dreamScreenIp { get; set; }
        private readonly IPEndPoint streamEndPoint;
        private IPEndPoint receiverPort;
        private UdpClient receiver;
        private readonly Socket sender;


        public DreamClient(DreamSync ds, DataObj dreamData) {
            dd = dreamData;
            devices = new List<BaseDevice>();
            receiver = new UdpClient();
            receiver.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            receiver.EnableBroadcast = true;
            dss = dreamData.MyDevice;
            string dsIp = dd.DsIp;
            dreamSync = ds;
            Console.WriteLine("Still alive");
            dreamScreenIp = IPAddress.Parse(dsIp);
            streamEndPoint = new IPEndPoint(dreamScreenIp, Port);
            deviceMode = dss.Mode;
            groupNumber = dss.GroupNumber;
            colors = new string[12];
            for (int i = 0; i < colors.Length; i++) {
                colors[i] = "FFFFFF";
            }
            // Create a listening socket
            sender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            sender.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
        }

        public void getMode() {
            requestState();
        }

        private void updateMode(int newMode) {
            if (deviceMode != newMode) {
                deviceMode = newMode;
                dss.Mode = newMode;
                dd.MyDevice = dss;
                DreamData.SaveJson(dd);
                Console.WriteLine("Updating mode to " + newMode.ToString());
                // If ambient, set to ambient colorS   
                bool sync = (newMode != 0);
                dreamSync.CheckSync(sync);
                if (newMode == 3) {
                    string cString = dd.MyDevice.AmbientColor[0].ToString("XX");
                    cString += dd.MyDevice.AmbientColor[1].ToString("XX");
                    cString += dd.MyDevice.AmbientColor[2].ToString("XX");

                    for (int i = 0; i < colors.Length; i++) {
                        colors[i] = cString;
                    }
                    Console.WriteLine("Updating color array to use ambient color: " + colors[0]);
                }
            }
        }


        public void subscribe() {
            Console.WriteLine("Subscribing to color data...");
            sendUDPWrite(0x01, 0x0C, new byte[] { 0x01 }, 0x10);
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
            string command = null;
            string flag = null;
            BaseDevice dss = null;
            string from = receivedIpEndPoint.Address.ToString();
            string[] payloadString = Array.Empty<string>();
            byte[] payload = Array.Empty<byte>();
            try {
                DreamScreenMessage msg = new DreamScreenMessage(receivedBytes, from);
                payload = msg.payload;
                payloadString = msg.payloadString;
                command = msg.command;                
                dss = msg.device;
                string[] ignore = { "SUBSCRIBE", "READ_CONNECT_VERSION?", "COLOR_DATA" };
                if (!ignore.Contains(command)) {
                    Console.WriteLine(from + " -> " + JsonConvert.SerializeObject(msg));
                }

                flag = msg.flags;
            } catch (Exception e) {
                Console.WriteLine("MSG parse Exception: " + e.Message);
            }

            switch (command) {
                case "SUBSCRIBE":
                    if (deviceMode != 0) {
                        sendUDPWrite(0x01, 0x0C, new byte[] { 0x01 }, 0x10); // Send sub response
                    }
                    break;
                case "COLOR_DATA":
                    if (deviceMode != 0) {
                        //Console.WriteLine("ColorData: " + payload);
                        IEnumerable<string> colorData = ByteUtils.SplitHex(string.Join("", payloadString), 6); // Swap this with payload
                        int lightCount = 0;
                        //Console.WriteLine("ColorData: " + JsonConvert.SerializeObject(colorData));
                        foreach (string colorValue in colorData) {
                            colors[lightCount] = colorValue;
                            lightCount++;
                            if (lightCount > 11) {
                                break;
                            }
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
                            Console.WriteLine("Adding devices: " + JsonConvert.SerializeObject(dss));
                            if (dd.DsIp == "0.0.0.0" && dss.Tag.Contains("DreamScreen")) {
                                dd.DsIp = from;
                            }

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
                    string gName = System.Text.Encoding.ASCII.GetString(payload);
                    dd.MyDevice.GroupName = gName;
                    DreamData.SaveJson(dd);
                    break;
                case "GROUP_NUMBER":
                    int gNum = payload[0];
                    dd.MyDevice.GroupNumber = gNum;
                    DreamData.SaveJson(dd);
                    break;
                case "NAME":
                    string dName = System.Text.Encoding.ASCII.GetString(payload);
                    dd.MyDevice.Name = dName;
                    DreamData.SaveJson(dd);
                    break;
                case "BRIGHTNESS":
                    dd.MyDevice.Brightness = payload[0];
                    DreamData.SaveJson(dd);
                    break;
                case "SATURATION":
                    dd.MyDevice.Saturation = Array.ConvertAll(payload, c => (int)c);
                    DreamData.SaveJson(dd);
                    break;
                case "MODE":
                    updateMode(payload[0]);
                    break;
                case "AMBIENT_MODE_TYPE":
                    dd.MyDevice.AmbientModeType = payload[0];
                    DreamData.SaveJson(dd);
                    break;
                case "AMBIENT_COLOR":
                    dd.MyDevice.AmbientColor = Array.ConvertAll(payload, c => (int)c);
                    DreamData.SaveJson(dd);
                    for (int i = 0; i < colors.Length; i++) {
                        colors[i] = payloadString.Join(string.Empty);
                    }
                    
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
            string pString = BitConverter.ToString(payload).Replace("-",string.Empty);
            Console.WriteLine("PAYLOAD: " + pString);
            groupNumber = ByteUtils.IntByte(dd.MyDevice.GroupNumber);
            sendUDPWrite(0x01, 0x0A, payload, 0x60, src);
        }


        public async Task<List<BaseDevice>> findDevices() {
            searching = true;
            devices = new List<BaseDevice>();
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
            devices = devices.Distinct().ToList();
            Console.WriteLine("Found Devices: " + JsonConvert.SerializeObject(devices));
            s.Stop();
            searching = false;

            return devices;
        }

        private void sendUDPWrite(byte command1, byte command2, byte[] payload, byte flag = 17, IPEndPoint ep = null) {
            if (ep == null) {
                ep = streamEndPoint;
            }

            using (MemoryStream stream = new MemoryStream()) {
                using (BinaryWriter response = new BinaryWriter(stream)) {
                    // Magic header
                    response.Write((byte)0xFC);
                    // Payload length
                    response.Write((byte)(payload.Length + 5));
                    // Group number
                    response.Write((byte)groupNumber);
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

                    byte[] byteSend = stream.ToArray();
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

        private byte[] buildPacket(byte command1, byte command2, byte[] payload, byte flag = 17, IPEndPoint ep = null) {
            if (ep == null) {
                ep = streamEndPoint;
            }

            using (MemoryStream stream = new MemoryStream()) {
                using (BinaryWriter response = new BinaryWriter(stream)) {
                    // Magic header
                    response.Write((byte)0xFC);
                    // Payload length
                    response.Write((byte)(payload.Length + 5));
                    // Group number
                    response.Write((byte)groupNumber);
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

                    byte[] byteSend = stream.ToArray();
                    // CRC
                    response.Write(CalculateCrc(byteSend));
                    string msg = BitConverter.ToString(stream.ToArray());
                    return stream.ToArray();

                }
            }
        }

        private void requestState() {

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
            DreamScreenMessage sm = new DreamScreenMessage(data, "localhost");
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
