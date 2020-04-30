using System.Collections.Generic;
using System.Threading.Tasks;
using HueDream.Models.Util;
using LifxNet;

namespace HueDream.Models.LIFX {
    public sealed class LifxDiscovery {
        private readonly LifxClient client;
        private List<LightBulb> bulbs;
        private bool disposed;

        public LifxDiscovery() {
            client = LifxSender.getClient();
        }

        public async Task<List<LifxData>> Discover(int timeOut) {
            bulbs = new List<LightBulb>();
            client.DeviceDiscovered += Client_DeviceDiscovered;
            client.StartDeviceDiscovery();
            LogUtil.Write("Starting discovery.");
            await Task.Delay(timeOut * 1000).ConfigureAwait(false);
            LogUtil.Write("Discovery completed.");
            client.StopDeviceDiscovery();
            var output = new List<LifxData>();
            foreach (var b in bulbs) {
                var state = client.GetLightStateAsync(b).Result;
                var d = new LifxData(b) {
                    Power = client.GetLightPowerAsync(b).Result,
                    Hue = state.Hue / 35565 * 360,
                    Saturation = (double) state.Saturation / 35565,
                    Brightness = (double) state.Brightness / 35565,
                    Kelvin = state.Kelvin,
                    SectorMapping = -1
                };
                output.Add(d);
            }

            return output;
        }

        public async Task<List<LifxData>> Refresh() {
            var b = await Discover(5).ConfigureAwait(false);
            var output = new List<LifxData>();
            var existing = DataUtil.GetItem<List<LifxData>>("lifxBulbs");
            foreach (LifxData bulb in b) {
                var add = true;
                if (existing != null) {
                    for (var i = 0; i < existing.Count; i++) {
                        LifxData ex = existing[i];
                        if (ex.MacAddressString != bulb.MacAddressString) continue;
                        add = false;
                        LogUtil.Write("Matching existing device, skipping...");
                        ex.LastSeen = bulb.LastSeen;
                        ex.SectorMapping = -1;
                        existing[i] = ex;
                    }
                }

                if (add) {
                    output.Add(bulb);
                }
            }

            if (existing != null) output.AddRange(existing);
            return output;
        }

        private void Client_DeviceDiscovered(object sender, LifxClient.DeviceDiscoveryEventArgs e) {
            var bulb = e.Device as LightBulb;
            LogUtil.Write("Bulb discovered?");
            bulbs.Add(bulb);
        }
    }
}