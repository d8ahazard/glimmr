using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using HueDream.Models.DreamScreen.Devices;

namespace HueDream.Models.DreamScreen {
    public static class DreamDiscovery {
        public static async Task<List<BaseDevice>> FindDevices() {
            var devices = new List<BaseDevice>();
            var msg = new byte[] {0xFC, 0x05, 0xFF, 0x30, 0x01, 0x0A, 0x2A};
            DreamSender.SendUdpBroadcast(msg);
            var s = new Stopwatch();
            s.Start();
            await Task.Delay(3000).ConfigureAwait(true);
            devices = devices.Distinct().ToList();
            s.Stop();
            DreamData.SetItem("devices", devices);
            return devices;
        }
    }
}