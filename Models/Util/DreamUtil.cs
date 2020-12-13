using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Net;
using System.Net.Sockets;

namespace Glimmr.Models.Util {
	public static class DreamUtil {
		 public static void SendSectors(List<Color> sectors, string id, int group) {
            if (sectors == null) throw new InvalidEnumArgumentException("Invalid sector list.");
            const byte flag = 0x3D;
            const byte c1 = 0x03;
            const byte c2 = 0x16;
            var p = new List<byte>();
            foreach (var col in sectors) {
                p.Add(ByteUtils.IntByte(col.R));
                p.Add(ByteUtils.IntByte(col.G));
                p.Add(ByteUtils.IntByte(col.B));
            }
            var ep = new IPEndPoint(IPAddress.Parse(id), 8888);
            SendUdpWrite(c1, c2, p.ToArray(), flag, (byte) group, ep);
        }
        
        public static void SetAmbientColor(Color color, string id, int group) {
            if (color == null) throw new InvalidEnumArgumentException("Invalid sector list.");
            byte flag = 0x11;
            byte c1 = 0x03;
            byte c2 = 0x05;
            var p = new List<byte>();
            p.Add(ByteUtils.IntByte(color.R));
            p.Add(ByteUtils.IntByte(color.G));
            p.Add(ByteUtils.IntByte(color.B));
            var ep = new IPEndPoint(IPAddress.Parse(id), 8888);
            SendUdpWrite(c1, c2, p.ToArray(), flag, (byte) group, ep);
        }
        public static void SendMessage(string command, dynamic value, string id) {
            var dev = DataUtil.GetDreamDevice(id);
            byte flag = 0x11;
            byte c1 = 0x03;
            byte c2 = 0x00;
            int v;
            var send = false;
            var payload = Array.Empty<byte>();
            var cFlags = MsgUtils.CommandBytes[command];
            if (cFlags != null) {
                c1 = cFlags[0];
                c2 = cFlags[1];
            }
            switch (command) {
                case "saturation":
                    c2 = 0x06;
                    payload = ByteUtils.StringBytes(value);
                    send = true;
                    break;
                case "minimumLuminosity":
                    c2 = 0x0C;
                    v = int.Parse(value);
                    payload = new[] {ByteUtils.IntByte(v), ByteUtils.IntByte(v), ByteUtils.IntByte(v)};
                    send = true;
                    break;
                case "ambientModeType":
                    if (cFlags != null) {
                        payload = new[] {ByteUtils.IntByte((int)value)};
                        c1 = cFlags[0];
                        c2 = cFlags[1];
                        send = true;
                    }
                    break;
                case "ambientScene":
                    if (cFlags != null) {
                        payload = new[] {ByteUtils.IntByte((int)value)};
                        c1 = cFlags[0];
                        c2 = cFlags[1];
                        send = true;
                    }
                    break;
            }

            if (send) {
                var ep = new IPEndPoint(IPAddress.Parse(dev.IpAddress), 8888);
                SendUdpWrite(c1, c2, payload, flag, (byte) dev.GroupNumber, ep, true);
            }
        }

        public static void SendUdpWrite(byte command1, byte command2, byte[] payload, byte flag = 17, byte group = 0,
            IPEndPoint ep = null, bool groupSend = false) {
            if (payload is null) throw new ArgumentNullException(nameof(payload));
            // If we don't specify an endpoint...talk to self
            ep ??= new IPEndPoint(IPAddress.Parse("0.0.0.0"), 8888);
            // Magic header
            // Payload length
            // Group number
            // Flag, should be 0x10 for subscription, 17 for everything else
            // Upper command
            // Lower command

            var msg = new List<byte> {
                0xFC,
                (byte) (payload.Length + 5),
                group,
                flag,
                command1,
                command2
            };
            // Payload
            msg.AddRange(payload);
            // CRC
            msg.Add(MsgUtils.CalculateCrc(msg.ToArray()));
            if (flag == 0x30 | groupSend) {
                SendUdpBroadcast(msg.ToArray());
                //if (cmd != "SUBSCRIBE" && cmd != "COLOR_DATA") LogUtil.Write($"localhost -> 255.255.255.255::{cmd} {flag}-{group}");
            } else {
                SendUdpUnicast(msg.ToArray(), ep);
                //if (cmd != "SUBSCRIBE" && cmd != "COLOR_DATA") LogUtil.Write($"localhost -> {ep.Address}::{cmd} {flag}-{group}");
            }
        }

        public static void SendUdpUnicast(byte[] data, EndPoint ep) {
            try {
                var dreamSender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                dreamSender.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                dreamSender.EnableBroadcast = true;
                dreamSender.SendTo(data, ep);
                dreamSender.Dispose();
            } catch (SocketException e) {
                LogUtil.Write($"Socket Exception: {e.Message}", "WARN");
            }
        }

        public static void SendUdpBroadcast(byte[] bytes) {
            if (bytes is null) throw new ArgumentNullException(nameof(bytes));
            try {
                var dreamClient = new UdpClient {Ttl = 128};
                dreamClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                var ip = new IPEndPoint(IPAddress.Parse("255.255.255.255"), 8888);
                dreamClient.Send(bytes, bytes.Length, ip);
                dreamClient.Dispose();
            } catch (SocketException e) {
                LogUtil.Write($"Socket Exception: {e.Message}", "WARN");
            }
        }
	}
}