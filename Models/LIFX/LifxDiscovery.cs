using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HueDream.Models.Util;
using LifxNet;

namespace HueDream.Models.LIFX {
    public sealed class LifxDiscovery {
        private readonly LifxClient client;
        private List<LightBulb> bulbs;

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
            return bulbs.Select(GetBulbInfo).ToList();
        }

        public async Task<List<LifxData>> Refresh() {
            var b = await Discover(5).ConfigureAwait(false);
            foreach (var bulb in b) {
                var existing = DataUtil.GetCollectionItem<LifxData>("lifxBulbs", bulb.MacAddressString);
                if (existing != null) {
                    bulb.SectorMapping = existing.SectorMapping;
                    bulb.MaxBrightness = existing.MaxBrightness;
                }
                DataUtil.InsertCollection<LifxData>("lifxBulbs", bulb);
            }

            return DataUtil.GetCollection<LifxData>("lifxBulbs");
        }

        private void Client_DeviceDiscovered(object sender, LifxClient.DeviceDiscoveryEventArgs e) {
            var bulb = e.Device as LightBulb;
            LogUtil.Write("Bulb discovered?");
            bulbs.Add(bulb);
        }

        private LifxData GetBulbInfo(LightBulb b) {
            var state = client.GetLightStateAsync(b).Result;
            var d = new LifxData(b) {
                Power = client.GetLightPowerAsync(b).Result,
                Hue = state.Hue / 35565 * 360,
                Saturation = (double) state.Saturation / 35565,
                Brightness = (double) state.Brightness / 35565,
                Kelvin = state.Kelvin,
                SectorMapping = -1
            };
            return d;
        }
    }
}