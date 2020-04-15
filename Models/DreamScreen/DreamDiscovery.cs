using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using HueDream.Models.DreamScreen.Devices;
using HueDream.Models.Util;

namespace HueDream.Models.DreamScreen {
    public static class DreamDiscovery {
        public static List<BaseDevice> FindDevices() {
            var devices = new List<BaseDevice>();
            var listenEndPoint = new IPEndPoint(IPAddress.Any, 8888);
            var listener = new UdpClient();
            listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            listener.EnableBroadcast = true;
            listener.Client.Bind(listenEndPoint);
            LogUtil.WriteInc("Listener started.");
            var msg = new byte[] {0xFC, 0x05, 0xFF, 0x30, 0x01, 0x0A, 0x2A};
            DreamSender.SendUdpBroadcast(msg);
            var s = new Stopwatch();
            s.Start();
            var t = TimeSpan.FromSeconds(5);
            while (s.Elapsed < t) {
                var sourceEndPoint = new IPEndPoint(IPAddress.Any, 0);
                var receivedResults = listener.Receive(ref sourceEndPoint);
                var dev = ProcessData(receivedResults, sourceEndPoint);
                if (dev != null) {
                    devices.Add(dev);
                }
            }
            devices = devices.Distinct().ToList();
            s.Stop();
            listener.Dispose();
            return devices;
        }
        
      


        private static BaseDevice ProcessData(byte[] receivedBytes, IPEndPoint receivedIpEndPoint) {
            // Convert data to ASCII and print in console
            if (!MsgUtils.CheckCrc(receivedBytes)) return null;
            string command = null;
            string flag = null;
            var from = receivedIpEndPoint.Address.ToString();
            BaseDevice msgDevice = null;
            var msg = new DreamScreenMessage(receivedBytes, from);
            if (msg.IsValid) {
                command = msg.Command;
                msgDevice = msg.Device;
                flag = msg.Flags;
            } else {
                LogUtil.Write("Invalid message?");
            }

            if (command != "DEVICE_DISCOVERY" || flag != "60") return null;
            return msgDevice;
        }
    }
}