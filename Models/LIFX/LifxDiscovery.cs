using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LifxNet;

namespace HueDream.Models.LIFX {
    public class LifxDiscovery {

        private LifxClient client;
        private List<LightBulb> bulbs;
        
        public LifxDiscovery() {
            client = LifxClient.CreateAsync().Result;
        }
        
        public async Task<List<LightBulb>> Discover(int timeOut) {
            bulbs = new List<LightBulb>();
            client.DeviceDiscovered += Client_DeviceDiscovered;
            var s = new Stopwatch();
            s.Start();
            client.StartDeviceDiscovery();
            while (s.ElapsedMilliseconds < timeOut * 1000) {
                
            }
            client.StopDeviceDiscovery();
            return bulbs;
        }

        private async void Client_DeviceDiscovered(object sender, LifxClient.DeviceDiscoveryEventArgs e) {
            var bulb = e.Device as LightBulb;
            bulbs.Add(bulb);
        }
    }
}