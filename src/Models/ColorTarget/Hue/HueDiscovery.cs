using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Q42.HueApi;
using Q42.HueApi.Interfaces;
using Q42.HueApi.Models.Bridge;
using Serilog;

namespace Glimmr.Models.ColorTarget.Hue {
    public class HueDiscovery : ColorDiscovery, IColorDiscovery {
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
            _controlService.AddDevice(data).ConfigureAwait(true);
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
        
        public static async Task<RegisterEntertainmentResult> CheckAuth(string bridgeIp) {
            try {
                ILocalHueClient client = new LocalHueClient(bridgeIp);
                //Make sure the user has pressed the button on the bridge before calling RegisterAsync
                //It will throw an LinkButtonNotPressedException if the user did not press the button
                var result = await client.RegisterAsync("Glimmr", Environment.MachineName, true)
                    .ConfigureAwait(false);
                return result;
            } catch (HueException) {
                Log.Debug($@"Hue: The link button is not pressed at {bridgeIp}.");
            }
            return null;
        }

        public override string DeviceTag { get; set; }
    }
}