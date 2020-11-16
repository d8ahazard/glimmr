using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Glimmr.Models.DreamScreen.Devices;
using Glimmr.Models.Util;

namespace Glimmr.Models.DreamScreen {
    public static class DreamDiscovery {
        public static async Task<List<BaseDevice>> Discover() {
            LogUtil.Write("Discovery started..");
            // Send a custom internal message to self to store discovery results
            var selfEp = new IPEndPoint(IPAddress.Loopback, 8888);
            DreamSender.SendUdpWrite(0x01, 0x0D, new byte[] {0x01}, 0x30, 0x00, selfEp);
            // Send our notification to actually discover
            var msg = new byte[] {0xFC, 0x05, 0xFF, 0x30, 0x01, 0x0A, 0x2A};
            DreamSender.SendUdpBroadcast(msg);
            await Task.Delay(3000).ConfigureAwait(false);
            DreamSender.SendUdpWrite(0x01, 0x0E, new byte[] {0x01}, 0x30, 0x00, selfEp);
            await Task.Delay(500).ConfigureAwait(false);
            var devices = DataUtil.GetDreamDevices();
            return devices;
        }
    }
}