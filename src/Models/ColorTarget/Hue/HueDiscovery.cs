using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Q42.HueApi;
using Q42.HueApi.Interfaces;
using Q42.HueApi.Models.Bridge;
using Serilog;

namespace Glimmr.Models.ColorTarget.Hue {
    public class HueDiscovery : ColorDiscovery, IColorDiscovery, IColorTargetAuth {
        private readonly BridgeLocator _bridgeLocatorHttp;
        private readonly BridgeLocator _bridgeLocatorSsdp;
        private readonly BridgeLocator _bridgeLocatorMdns;
        private readonly ControlService _controlService;
        public HueDiscovery(ColorService colorService) : base(colorService) {
            _bridgeLocatorHttp = new HttpBridgeLocator();
            _bridgeLocatorMdns = new MdnsBridgeLocator();
            _bridgeLocatorSsdp = new SsdpBridgeLocator();
            _bridgeLocatorHttp.BridgeFound += DeviceFound;
            _bridgeLocatorMdns.BridgeFound += DeviceFound;
            _bridgeLocatorSsdp.BridgeFound += DeviceFound;
            DeviceTag = "Hue";
            _controlService = colorService.ControlService;
        }

        private void DeviceFound(IBridgeLocator sender, LocatedBridge locatedbridge) {
            var data = new HueData(locatedbridge);
            data = UpdateDeviceData(data);
            _controlService.AddDevice(data).ConfigureAwait(false);
        }

        private static HueData UpdateDeviceData(HueData data) {
            // Check for existing device
            HueData dev = DataUtil.GetDevice<HueData>(data.Id);
            if (dev != null && !string.IsNullOrEmpty(dev.Token)) {
                Log.Debug($"Updating bridge: {data.IpAddress}");
                var client = new LocalHueClient(data.IpAddress, dev.User, dev.Token);
                try {
                    Log.Debug("Client is initialized?");
                    var groups = client.GetGroupsAsync().Result;
                    var lights = client.GetLightsAsync().Result;
                    Log.Debug("Groups: " + JsonConvert.SerializeObject(groups));
                    Log.Debug("Lights: " + JsonConvert.SerializeObject(lights));
                    data.AddGroups(groups);
                    data.AddLights(lights);
                    dev.UpdateFromDiscovered(data);
                    data = dev;
                    Log.Debug("Final device: " + JsonConvert.SerializeObject(data));
                } catch (Exception e) {
                    Log.Warning("Exception: " + e.Message);
                }
            }
            return data;
        }

        public async Task Discover(CancellationToken ct) {
            Log.Debug("Hue: Discovery started...");
            try {
                await Task.WhenAll(_bridgeLocatorHttp.LocateBridgesAsync(ct), _bridgeLocatorMdns.LocateBridgesAsync(ct),
                    _bridgeLocatorSsdp.LocateBridgesAsync(ct));
                
            } catch (Exception e) {
                Log.Debug("Hue discovery exception: " + e.Message);
            }

            Log.Debug("Hue: Discovery complete.");
        }

        public async Task<dynamic> CheckAuthAsync(dynamic devData) {
            try {
                ILocalHueClient client = new LocalHueClient(devData.IpAddress);
                //Make sure the user has pressed the button on the bridge before calling RegisterAsync
                //It will throw an LinkButtonNotPressedException if the user did not press the button
                var result = await client.RegisterAsync("Glimmr", Environment.MachineName, true);
                if (result == null) {
                    return devData;
                }

                if (string.IsNullOrEmpty(result.Username) || string.IsNullOrEmpty(result.StreamingClientKey)) {
                    return devData;
                }

                devData.Token = result.StreamingClientKey;
                devData.User = result.Username;
                devData = UpdateDeviceData(devData);
                return devData;
            } catch (HueException) {
                Log.Debug($@"Hue: The link button is not pressed at {devData.IpAddress}.");
            }
            return devData;
        }

        public override string DeviceTag { get; set; }
        
        
    }
}