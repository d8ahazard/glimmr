using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using HueDream.Models.Util;
using Newtonsoft.Json;

namespace HueDream.Models.DreamScreen {
    public static class DreamSender {

        public static void SendMessage(string command, dynamic value, string id) {
            var dev = DataUtil.GetDreamDevice(id);
            byte flag = 0x11;
            byte c1 = 0x03;
            byte c2 = 0x00;
            int v;
            var payload = Array.Empty<byte>();
            switch (command) {
                case "saturation":
                    c2 = 0x06;
                    payload = ByteUtils.StringBytes(value);
                    break;
                case "minimumLuminosity":
                    c2 = 0x0C;
                    v = int.Parse(value);
                    payload = new []{ByteUtils.IntByte(v)};
                    break;
                case "letterboxingEnable":
                    c2 = 0x2B;
                    v = int.Parse(value);
                    payload = new []{ByteUtils.IntByte(v)};
                    break;
                case "colorBoost":
                    c2 = 0x2D;
                    v = int.Parse(value);
                    payload = new []{ByteUtils.IntByte(v)};
                    break;
                case "fadeRate":
                    c2 = 0x0E;
                    v = int.Parse(value);
                    payload = new []{ByteUtils.IntByte(v)};
                    break;
            }
            SendUdpWrite(c1, c2, payload, flag, (byte)dev.GroupNumber, IPEndPoint.Parse(dev.IpAddress));
        }
        public static void SendUdpWrite(byte command1, byte command2, byte[] payload, byte flag = 17, byte group = 0,
            IPEndPoint ep = null, bool groupSend = false) {
            if (payload is null) throw new ArgumentNullException(nameof(payload));
            // If we don't specify an endpoint...talk to self
            ep ??= new IPEndPoint(IPAddress.Parse("0.0.0.0"), 8888);
            using var stream = new MemoryStream();
            using var response = new BinaryWriter(stream);
            // Magic header
            response.Write((byte) 0xFC);
            // Payload length
            response.Write((byte) (payload.Length + 5));
            // Group number
            response.Write(group);
            // Flag, should be 0x10 for subscription, 17 for everything else
            response.Write(flag);
            // Upper command
            response.Write(command1);
            // Lower command
            response.Write(command2);
            // Payload
            foreach (var b in payload) response.Write(b);
            var byteSend = stream.ToArray();
            // CRC
            response.Write(MsgUtils.CalculateCrc(byteSend));
            var byteString = BitConverter.ToString(stream.ToArray());
            var bytesString = byteString.Split("-");
            var cmd = bytesString[4] + bytesString[5];
            cmd = MsgUtils.Commands[cmd] ?? cmd;
            if (cmd == "SATURATION") LogUtil.Write("BYTE STRING: " + byteString);
            if (flag == 0x30 | groupSend) {
                SendUdpBroadcast(stream.ToArray());
                if (cmd != "SUBSCRIBE") LogUtil.Write($"localhost -> 255.255.255.255::{cmd}");
            } else {
                SendUdpUnicast(stream.ToArray(), ep);
                if (cmd != "SUBSCRIBE") LogUtil.Write($"localhost -> {ep.Address}::{cmd} {flag} {group}");
            }
        }

        private static void SendUdpUnicast(byte[] data, EndPoint ep) {
            var sender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            sender.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            sender.EnableBroadcast = true;
            sender.SendTo(data, ep);
            sender.Dispose();
        }

        public static void SendUdpBroadcast(byte[] bytes) {
            if (bytes is null) throw new ArgumentNullException(nameof(bytes));
            var client = new UdpClient {Ttl = 128};
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            var ip = new IPEndPoint(IPAddress.Parse("255.255.255.255"), 8888);
            client.Send(bytes, bytes.Length, ip);
            client.Dispose();
        }
    }
}