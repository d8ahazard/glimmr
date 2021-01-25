using System.Threading.Tasks;
using Glimmr.Models.Util;
using LifxNet;
using Serilog;

namespace Glimmr.Models.ColorTarget.LIFX {
    public class LifxDiscovery {
        private readonly LifxClient _client;
        
        public LifxDiscovery(LifxClient client) {
            _client = client;
        }

        public async Task Discover(int timeOut = 5) {
            if (_client == null) return;
            Log.Debug("Lifx: Discovery started.");
            _client.DeviceDiscovered += Client_DeviceDiscovered;
            _client.StartDeviceDiscovery();
            await Task.Delay(timeOut * 1000);
            _client.StopDeviceDiscovery();
            Log.Debug("Lifx: Discovery complete.");
        }
        
        private void Client_DeviceDiscovered(object sender, LifxClient.DeviceDiscoveryEventArgs e) {
            var bulb = e.Device as LightBulb;
            var ld = GetBulbInfo(bulb);
            var existing = DataUtil.GetCollectionItem<LifxData>("Dev_Lifx", ld.MacAddressString);
            if (existing != null) {
                ld.TargetSector = existing.TargetSector;
                ld.Brightness = existing.Brightness;
            }
            DataUtil.InsertCollection<LifxData>("Dev_Lifx", ld).ConfigureAwait(false);
        }

        private LifxData GetBulbInfo(LightBulb b) {
            var state = _client.GetLightStateAsync(b).Result;
            var d = new LifxData(b) {
                Power = _client.GetLightPowerAsync(b).Result,
                Hue = state.Hue,
                Saturation = state.Saturation,
                Brightness = state.Brightness,
                Kelvin = state.Kelvin,
                TargetSector = -1
            };
            return d;
        }
    }
}