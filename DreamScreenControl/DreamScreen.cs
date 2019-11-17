using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace HueDream.DreamScreenControl {
    class DreamScreen {
        private static byte[] crc8_table = new byte[] { unchecked((byte)0), unchecked((byte)7), unchecked((byte)14), unchecked((byte)9), unchecked((byte)28), unchecked((byte)27), unchecked((byte)18), unchecked((byte)21), unchecked((byte)56), unchecked((byte)63), unchecked((byte)54), unchecked((byte)49),
            unchecked((byte)36), unchecked((byte)35), unchecked((byte)42), unchecked((byte)45), unchecked((byte)112), unchecked((byte)119), unchecked((byte)126), unchecked((byte)121), unchecked((byte)108), unchecked((byte)107), unchecked((byte)98), unchecked((byte)101), unchecked((byte)72), unchecked((byte)79),
            unchecked((byte)70), unchecked((byte)65), unchecked((byte)84), unchecked((byte)83), unchecked((byte)90), unchecked((byte)93), unchecked((byte)-32), unchecked((byte)-25), unchecked((byte)-18), unchecked((byte)-23), unchecked((byte)-4), unchecked((byte)-5), unchecked((byte)-14), unchecked((byte)-11),
            unchecked((byte)-40), unchecked((byte)-33), unchecked((byte)-42), unchecked((byte)-47), unchecked((byte)-60), unchecked((byte)-61), unchecked((byte)-54), unchecked((byte)-51), unchecked((byte)-112), unchecked((byte)-105), unchecked((byte)-98), unchecked((byte)-103), unchecked((byte)-116), unchecked((byte)-117),
            unchecked((byte)-126), unchecked((byte)-123), unchecked((byte)-88), unchecked((byte)-81), unchecked((byte)-90), unchecked((byte)-95), unchecked((byte)-76), unchecked((byte)-77), unchecked((byte)-70), unchecked((byte)-67), unchecked((byte)-57), unchecked((byte)-64), unchecked((byte)-55), unchecked((byte)-50),
            unchecked((byte)-37), unchecked((byte)-36), unchecked((byte)-43), unchecked((byte)-46), unchecked((byte)-1), unchecked((byte)-8), unchecked((byte)-15), unchecked((byte)-10), unchecked((byte)-29), unchecked((byte)-28), unchecked((byte)-19), unchecked((byte)-22), unchecked((byte)-73), unchecked((byte)-80),
            unchecked((byte)-71), unchecked((byte)-66), unchecked((byte)-85), unchecked((byte)-84), unchecked((byte)-91), unchecked((byte)-94), unchecked((byte)-113), unchecked((byte)-120), unchecked((byte)-127), unchecked((byte)-122), unchecked((byte)-109), unchecked((byte)-108), unchecked((byte)-99), unchecked((byte)-102),
            unchecked((byte)39), unchecked((byte)32), unchecked((byte)41), unchecked((byte)46), unchecked((byte)59), unchecked((byte)60), unchecked((byte)53), unchecked((byte)50), unchecked((byte)31), unchecked((byte)24), unchecked((byte)17), unchecked((byte)22), unchecked((byte)3), unchecked((byte)4), unchecked((byte)13),
            unchecked((byte)10), unchecked((byte)87), unchecked((byte)80), unchecked((byte)89), unchecked((byte)94), unchecked((byte)75), unchecked((byte)76), unchecked((byte)69), unchecked((byte)66), unchecked((byte)111), unchecked((byte)104), unchecked((byte)97), unchecked((byte)102), unchecked((byte)115), unchecked((byte)116),
            unchecked((byte)125), unchecked((byte)122), unchecked((byte)-119), unchecked((byte)-114), unchecked((byte)-121), 0, unchecked((byte)-107), unchecked((byte)-110), unchecked((byte)-101), unchecked((byte)-100), unchecked((byte)-79), unchecked((byte)-74), unchecked((byte)-65), unchecked((byte)-72), unchecked((byte)-83),
            unchecked((byte)-86), unchecked((byte)-93), unchecked((byte)-92), unchecked((byte)-7), unchecked((byte)-2), unchecked((byte)-9), unchecked((byte)-16), unchecked((byte)-27), unchecked((byte)-30), unchecked((byte)-21), unchecked((byte)-20), unchecked((byte)-63), unchecked((byte)-58), unchecked((byte)-49), unchecked((byte)-56),
            unchecked((byte)-35), unchecked((byte)-38), unchecked((byte)-45), unchecked((byte)-44), unchecked((byte)105), unchecked((byte)110), unchecked((byte)103), unchecked((byte)96), unchecked((byte)117), unchecked((byte)114), unchecked((byte)123), unchecked((byte)124), unchecked((byte)81), unchecked((byte)86), unchecked((byte)95),
            unchecked((byte)88), unchecked((byte)77), unchecked((byte)74), unchecked((byte)67), unchecked((byte)68), unchecked((byte)25), unchecked((byte)30), unchecked((byte)23), unchecked((byte)16), unchecked((byte)5), unchecked((byte)2), unchecked((byte)11), unchecked((byte)12), unchecked((byte)33), unchecked((byte)38),
            unchecked((byte)47), unchecked((byte)40), unchecked((byte)61), unchecked((byte)58), unchecked((byte)51), unchecked((byte)52), unchecked((byte)78), unchecked((byte)73), unchecked((byte)64), unchecked((byte)71), unchecked((byte)82), unchecked((byte)85), unchecked((byte)92), unchecked((byte)91), unchecked((byte)118),
            unchecked((byte)113), unchecked((byte)120), (byte)255, unchecked((byte)106), unchecked((byte)109), unchecked((byte)100), unchecked((byte)99), unchecked((byte)62), unchecked((byte)57), unchecked((byte)48), unchecked((byte)55), unchecked((byte)34), unchecked((byte)37), unchecked((byte)44), unchecked((byte)43),
            unchecked((byte)6), unchecked((byte)1), unchecked((byte)8), unchecked((byte)15), unchecked((byte)26), unchecked((byte)29), unchecked((byte)20), unchecked((byte)19), unchecked((byte)-82), unchecked((byte)-87), unchecked((byte)-96), unchecked((byte)-89), unchecked((byte)-78), unchecked((byte)-75), unchecked((byte)-68),
            unchecked((byte)-69), unchecked((byte)-106), unchecked((byte)-111), unchecked((byte)-104), unchecked((byte)-97), unchecked((byte)-118), unchecked((byte)-115), unchecked((byte)-124), unchecked((byte)-125), unchecked((byte)-34), unchecked((byte)-39), unchecked((byte)-48), unchecked((byte)-41), unchecked((byte)-62),
            unchecked((byte)-59), unchecked((byte)-52), unchecked((byte)-53), unchecked((byte)-26), unchecked((byte)-31), unchecked((byte)-24), unchecked((byte)-17), unchecked((byte)-6), unchecked((byte)-3), unchecked((byte)-12), unchecked((byte)-13) };

        public IPAddress dreamScreenIp { get; set; }
        public string[][] colors { get; }
        private Socket dreamScreenSocket;
        private int Port = 8888;
        private IPEndPoint endPoint;
        private byte groupNumber = 0;
        private UdpClient receiver;
        public bool listening = false;


        public DreamScreen(string remoteIP) {
            dreamScreenIp = IPAddress.Parse(remoteIP);
            endPoint = new IPEndPoint(dreamScreenIp, Port);
            groupNumber = 1;
            colors = new string[12][];
            for (int i = 0; i < colors.Length; i++) {
                colors[i] = new string[] { "FF", "FF", "FF" };
            }
            dreamScreenSocket = new Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp);
            dreamScreenSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, (int)1);
            getMode();
        }

        public void setMode(int mode) {
            sendUDPWrite((byte)3, (byte)1, new byte[] { (byte)mode });
        }

        public void getMode() {
            Console.WriteLine("Trying to get state...");
            byte[] payload = Array.Empty<byte>();
            requestState();
            Listen();
        }

        public void startListening() {
            sendUDPWrite((byte)0x01, (byte)0x0C, new byte[] { (byte)0x01 }, (byte)0x10);
            Listen();
        }

        public void Listen() {
            if (!listening) {
                // Create UDP client
                IPEndPoint receiverPort = new IPEndPoint(IPAddress.Any, 8888); ;
                receiver = new UdpClient();
                receiver.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                receiver.EnableBroadcast = true;
                receiver.Client.Bind(receiverPort);
                Console.WriteLine("Starting DreamScreen Upd receiving on port: " + receiverPort);
                // Call DataReceived() every time it gets something
                listening = true;
                receiver.BeginReceive(DataReceived, receiver);
            }
        }

        public void StopListening() {
            if (listening) {
                dreamScreenSocket.Close();
                listening = false;
            }
        }

        private void DataReceived(IAsyncResult ar) {
            UdpClient c = (UdpClient)ar.AsyncState;
            //c.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            IPEndPoint receivedIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
            Byte[] receivedBytes = c.EndReceive(ar, ref receivedIpEndPoint);
            // Convert data to ASCII and print in console
            string receivedText = Encoding.ASCII.GetString(receivedBytes);
            string byteString = BitConverter.ToString(receivedBytes);
            string[] bytesString = byteString.Split("-");
            // This is hacky as hell
            if (byteString == "FC-05-01-30-01-0C-FF") {
                try {
                    sendUDPWrite((byte)0x01, (byte)0x0C, new byte[] { (byte)0x01 }, (byte)0x10);
                } catch (Exception e) {
                    Console.WriteLine(e.ToString());
                }
                // So is this, but it works.
            } else if ($"{bytesString[4]}{bytesString[5]}" == "0316") {
                IEnumerable<string> colorData = bytesString.Skip(6);
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
            } else {
                Console.WriteLine("State: " + byteString);
                DreamScreenState dState = new DreamScreenState(byteString);
                Console.WriteLine("Device State: " + JsonConvert.SerializeObject(dState));
            }

            // Restart listening for udp data packages
            c.BeginReceive(DataReceived, ar.AsyncState);
        }


        public List<string> findDevices() {
            List<string> devices = new List<string>();
            byte[] payload = new byte[1];
            payload[0] = (byte)1;
            int PORT = 8888;
            UdpClient udpClient = new UdpClient();
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, PORT));

            var from = new IPEndPoint(0, 0);

            bool noDevice = true;
            int count = 0;
            while (noDevice & count < 5) {
                var recvBuffer = udpClient.Receive(ref from);
                string localAddress = GetLocalIPAddress() + ":8888";
                if (from.ToString() != localAddress) {
                    devices.Add(from.ToString());
                    noDevice = false;
                }
                count++;
            }
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


        void sendUDPWrite(byte command1, byte command2, byte[] payload, byte flag = (byte)17) {

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
                    response.Write(calculateCrc(byteSend));
                    string msg = BitConverter.ToString(stream.ToArray());
                    sendUDPUnicast(stream.ToArray());
                }
            }
        }

        void requestState() {

            using (MemoryStream stream = new MemoryStream()) {
                // : FC:05:FF:30:01:0A:2A
                using (BinaryWriter response = new BinaryWriter(stream)) {
                    // Magic header
                    response.Write((byte)0xFC);
                    response.Write((byte)0x05);
                    response.Write((byte)0xFF);
                    response.Write((byte)0x30);
                    response.Write((byte)0x01);
                    response.Write((byte)0x0A);
                    response.Write((byte)0x2A);
                    var byteSend = stream.ToArray();
                    // CRC
                    response.Write(calculateCrc(byteSend));
                    string msg = BitConverter.ToString(stream.ToArray());
                    sendUDPUnicast(stream.ToArray());
                }
            }
        }

        private byte calculateCrc(byte[] data) {
            byte size = (byte)(data[1] + 1);
            byte crc = (byte)0;
            for (byte cntr = (byte)0; cntr < size; cntr = (byte)(cntr + 1)) {
                crc = crc8_table[((byte)(data[cntr] ^ crc)) & 255];
            }
            return crc;
        }

        private void sendUDPUnicast(byte[] data) {
            dreamScreenSocket.SendTo(data, endPoint);
        }

        class DreamScreenState {
            public string deviceType;
            public string deviceState;
            public DreamScreenState(string byteString) {
                string[] bytesIn = byteString.Split("-");
                string magic = bytesIn[0];
                string typeString = "Unknown";
                string deviceState = "Unknown";

                if (magic == "FC") {
                    string type = bytesIn[bytesIn.Length - 2];
                    string state = bytesIn[33];
                    
                    if (type == "01") {
                        typeString = "DreamScreen";
                    } else if (type == "02") {
                        typeString = "DreamScreen 4K";
                    } else if (type == "03") {
                        typeString = "Sidekick";
                    }
                    deviceType = typeString;
                    if (state == "00") {
                        deviceState = "Sleep";
                    } else if (state == "01") {
                        deviceState = "Video";
                    } else if (state == "02") {
                        deviceState = "Music";
                    } else if (state == "03") {
                        deviceState = "Ambient";
                    }
                }
                Console.WriteLine("Type is " + typeString + ", state is " + deviceState);
            }
        }
    }

    
}
