using HueDream.Util;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace HueDream.DreamScreen.Devices {
    public static class DreamSender {

        public static void SendUDPWrite(byte command1, byte command2, byte[] payload, byte flag = 17, byte group = 0, IPEndPoint ep = null) {
            // If we don't specify an endpoint...talk to ourself
            if (ep == null) {
                ep = new IPEndPoint(IPAddress.Parse("0.0.0.0"), 8888);
            }

            using (MemoryStream stream = new MemoryStream()) {
                using (BinaryWriter response = new BinaryWriter(stream)) {
                    // Magic header
                    response.Write((byte)0xFC);
                    // Payload length
                    response.Write((byte)(payload.Length + 5));
                    // Group number
                    response.Write(group);
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
                    response.Write(MsgUtils.CalculateCrc(byteSend));
                    string msg = BitConverter.ToString(stream.ToArray());
                    if (flag == 0x30) {
                        SendUDPBroadcast(stream.ToArray());
                    } else {
                        SendUDPUnicast(stream.ToArray(), ep);
                    }

                }
            }
        }

        public static void SendUDPUnicast(byte[] data, IPEndPoint ep) {
            Socket sender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            sender.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
            string byteString = BitConverter.ToString(data);
            DreamScreenMessage sm = new DreamScreenMessage(data, "localhost");
            //Console.WriteLine("localhost:8888 -> " + ep.ToString() + " " + JsonConvert.SerializeObject(sm));
            sender.EnableBroadcast = false;
            sender.SendTo(data, ep);
        }




        public static void SendUDPBroadcast(byte[] bytes) {
            UdpClient client = new UdpClient();
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            IPEndPoint ip = new IPEndPoint(IPAddress.Parse("255.255.255.255"), 8888);
            client.Send(bytes, bytes.Length, ip);
            Console.WriteLine("SENT");
        }

    }
}
