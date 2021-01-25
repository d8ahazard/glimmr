using System;
using System.Linq;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Q42.HueApi;
using Q42.HueApi.Interfaces;
using Q42.HueApi.Models.Bridge;
using Serilog;

namespace Glimmr.Models.ColorTarget.Hue {
    public static class HueDiscovery {
        
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

        public static async Task Discover(int timeOut=5) {
            Log.Debug("Hue: Discovery started...");
            try {
                var span = TimeSpan.FromSeconds(timeOut);
                var discovered = await HueBridgeDiscovery.CompleteDiscoveryAsync(span, span);
                var output = discovered.Select(bridge => new HueData(bridge)).ToList();
                foreach (var dev in output) {
                    var copy = dev;
                    var existing = DataUtil.GetCollectionItem<HueData>("Dev_Hue", dev.Id);
                    if (existing != null) {
                        copy.CopyBridgeData(existing);
                        if (copy.Key !=  null && copy.User != null) {
                            var n = new HueDevice(copy);
                            copy = n.RefreshData().Result;
                            n.Dispose();
                        }
                    }
                    await DataUtil.InsertCollection<HueData>("Dev_Hue", copy);
                }
            } catch (Exception e) {
                Log.Warning("Hue: Discovery exception: " + e.Message);
            }

            Log.Debug("Hue: Discovery complete.");
        }


    }
}