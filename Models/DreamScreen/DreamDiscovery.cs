using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using HueDream.Models.DreamScreen.Devices;
using HueDream.Models.Util;
using Newtonsoft.Json;

namespace HueDream.Models.DreamScreen {
    public static class DreamDiscovery {
        public static List<BaseDevice> FindDevices() {
            LogUtil.Write("Discovery started..");
            // Send a custom internal message to ourself to store discovery results
            var selfEp = new IPEndPoint(IPAddress.Loopback, 8888);
            DreamSender.SendUdpWrite(0x01, 0x0D,new byte[]{0x01},0x30,0x00, selfEp);
            // Send our notification to actually discover
            var msg = new byte[] {0xFC, 0x05, 0xFF, 0x30, 0x01, 0x0A, 0x2A};
            DreamSender.SendUdpBroadcast(msg);
            var s = new Stopwatch();
            s.Start();
            var t = TimeSpan.FromSeconds(5);
            while (s.Elapsed < t) {}
            s.Stop();
            DreamSender.SendUdpWrite(0x01, 0x0E,new byte[]{0x01},0x30,0x00, selfEp);
            var devices = DreamData.GetDreamDevices();
            LogUtil.Write($"Discovery complete, found {devices.Count} devices.");
            return devices;
        }
    }
}