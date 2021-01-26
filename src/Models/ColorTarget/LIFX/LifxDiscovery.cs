using System;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using LifxNet;
using Serilog;

namespace Glimmr.Models.ColorTarget.LIFX {
    public class LifxDiscovery {
        private readonly LifxClient _client;
        
        public LifxDiscovery(ControlService cs) {
            _client = cs.LifxClient;
        }

        public async Task Discover(CancellationToken ct) {
            if (_client == null) return;
            Log.Debug("Lifx: Discovery started.");
            _client.DeviceDiscovered += Client_DeviceDiscovered;
            _client.StartDeviceDiscovery();
            while (!ct.IsCancellationRequested) {
                await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None);
            }
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