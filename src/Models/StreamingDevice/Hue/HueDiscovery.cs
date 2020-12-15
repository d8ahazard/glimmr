using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Newtonsoft.Json;
using Q42.HueApi;
using Q42.HueApi.Interfaces;
using Q42.HueApi.Models.Bridge;
using Serilog;

namespace Glimmr.Models.StreamingDevice.Hue {
    public static class HueDiscovery {
        
        public static async Task<List<HueData>> Refresh(CancellationToken ct) {
            var output = new List<HueData>();
            var foo = Task.Run(() => Discover(), ct);
            var newBridges = await foo;
            Log.Debug("New bridges: " + JsonConvert.SerializeObject(newBridges));
            foreach (var nb in newBridges) {
                HueData ex = DataUtil.GetCollectionItem<HueData>("Dev_Hue", nb.Id);
                if (ex != null) {
                    ex.CopyBridgeData(nb);
                    output.Add(ex);
                } else {
                    output.Add(nb);   
                }
            }

            return output;
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

        public static async Task<List<HueData>> Discover(int time = 10) {
            Log.Debug("Hue: Discovery Started.");
            var output = new List<HueData>();
            try {
                var discovered = await HueBridgeDiscovery.CompleteDiscoveryAsync(TimeSpan.FromSeconds(time),TimeSpan.FromSeconds(time));
                output = discovered.Select(bridge => new HueData(bridge)).ToList();
                Log.Debug($"Hue: Discovery complete, found {discovered.Count} devices.");
            } catch (Exception e) {
                Log.Warning("Discovery exception: " + e.Message);
                
            }

            return output;
        }


    }
}