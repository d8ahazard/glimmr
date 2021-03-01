using System;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Services;
using LifxNet;
using Newtonsoft.Json;
using Serilog;

namespace Glimmr.Models.ColorTarget.Lifx {
    public class LifxDiscovery : ColorDiscovery, IColorDiscovery {
        private readonly LifxClient _client;
        private ControlService _controlService;
        
        public LifxDiscovery(ColorService cs) : base(cs) {
            _client = cs.ControlService.GetAgent("LifxAgent");
            _client.DeviceDiscovered += Client_DeviceDiscovered;
            _controlService = cs.ControlService;
            DeviceTag = "Lifx";
        }

        public async Task Discover(CancellationToken ct) {
            if (_client == null) return;
            Log.Debug("Lifx: Discovery started.");
            _client.StartDeviceDiscovery();
            while (!ct.IsCancellationRequested) {
                await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None);
            }
            _client.StopDeviceDiscovery();
            Log.Debug("Lifx: Discovery complete.");
        }
        
        private async void Client_DeviceDiscovered(object sender, LifxClient.DeviceDiscoveryEventArgs e) {
            var bulb = e.Device as LightBulb;
            Log.Debug("Device found: " + JsonConvert.SerializeObject(bulb));
            var ld = await GetBulbInfo(bulb);
            await _controlService.AddDevice(ld);
        }

        private async Task<LifxData> GetBulbInfo(LightBulb b) {
            Log.Debug("Getting state...");
            var state = await _client.GetLightStateAsync(b);
            Log.Debug("State retrieved: " + JsonConvert.SerializeObject(state));
            var ver = await _client.GetDeviceVersionAsync(b);
            Log.Debug("Lifx Version got: " + JsonConvert.SerializeObject(ver));
            var hasMulti = false;
            var v2 = false;
            var zoneCount = 0;
            // Set multi zone stuff
            if (ver.Product == 31 || ver.Product == 32 || ver.Product == 38) {
                hasMulti = true;
                if (ver.Product != 31) {
                    var fw = _client.GetDeviceHostFirmwareAsync(b).Result;
                    if (fw.Version >= 1532997580) {
                        v2 = true;
                    }
                }

                // var zoneData = _client.GetColorZonesAsync(b, 0, 8).Result;
                // if (zoneData is StateZoneResponse s1) {
                //     zoneCount = s1.Count;
                // } else {
                //     var s = (StateMultiZoneResponse) zoneData;
                //     zoneCount = s.Count;
                // }
            }
            var d = new LifxData(b) {
                Power = state.IsOn,
                Hue = state.Hue,
                Saturation = state.Saturation,
                Brightness = state.Brightness,
                Kelvin = state.Kelvin,
                TargetSector = -1,
                HasMultiZone = hasMulti,
                MultiZoneV2 = v2,
                MultiZoneCount = zoneCount
            };
            Log.Debug("Discovered lifx device: " + JsonConvert.SerializeObject(d));
            return d;
        }

        public override string DeviceTag { get; set; }
    }
}