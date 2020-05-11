using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using HueDream.Models.Util;
using Q42.HueApi;
using Q42.HueApi.Interfaces;
using Q42.HueApi.Models.Bridge;
using Q42.HueApi.Models.Groups;
using Q42.HueApi.Streaming;

namespace HueDream.Models.StreamingDevice.Hue {
    public class HueDiscovery {

        
         private static async Task<List<Group>> ListGroups(StreamingHueClient client) {
            var all = await client.LocalHueClient.GetEntertainmentGroups();
            var output = new List<Group>();
            output.AddRange(all);
            LogUtil.Write("Listed");
            return output;
        }


        public static async Task<RegisterEntertainmentResult> CheckAuth(string bridgeIp) {
            try {
                ILocalHueClient client = new LocalHueClient(bridgeIp);
                //Make sure the user has pressed the button on the bridge before calling RegisterAsync
                //It will throw an LinkButtonNotPressedException if the user did not press the button
                var result = await client.RegisterAsync("HueDream", Environment.MachineName, true)
                    .ConfigureAwait(false);
                return result;
            } catch (HueException) {
                LogUtil.Write($@"Hue: The link button is not pressed at {bridgeIp}.");
            }

            return null;
        }

        public static async Task<List<BridgeData>> Refresh() {
            var newBridges = await Discover().ConfigureAwait(false);
            foreach (var nb in newBridges) {
                var ex = DataUtil.GetCollectionItem<BridgeData>("bridges", nb.Id);
                LogUtil.Write("Looping for bridge...");
                if (ex != null) nb.CopyBridgeData(ex);
                if (nb.Key != null && nb.User != null) {
                    try {
                        using var client = StreamingSetup.GetClient(nb);
                        LogUtil.Write("Refreshing bridge: " + nb.Id);
                        nb.SetLights(GetLights(nb, client));
                        nb.SetGroups(ListGroups(client).Result);
                    } catch (SocketException e) {
                        LogUtil.Write("Socket Exception: " + e.Message, "ERROR");
                    }
                }
                DataUtil.InsertCollection<BridgeData>("bridges", nb);
            }

            return DataUtil.GetCollection<BridgeData>("bridges");
        }

        public static async Task<List<BridgeData>> Discover(int time = 3) {
            LogUtil.Write("Hue: Discovery Started.");
            var discovered = await HueBridgeDiscovery
                .FastDiscoveryWithNetworkScanFallbackAsync(TimeSpan.FromSeconds(time), TimeSpan.FromSeconds(7))
                .ConfigureAwait(false);

            var output = discovered.Select(bridge => new BridgeData(bridge)).ToList();

            LogUtil.Write($"Hue: Discovery complete, found {discovered.Count} devices.");
            return output;
        }


        private static List<LightData> GetLights(BridgeData bd, StreamingHueClient client) {
            if (bd == null || client == null) throw new ArgumentException("Invalid argument.");
            // If we have no IP or we're not authorized, return
            if (bd.IpAddress == "0.0.0.0" || bd.User == null || bd.Key == null) return new List<LightData>();
            // Create client
            client.LocalHueClient.Initialize(bd.User);
            // Get lights
            var lights = bd.Lights ?? new List<LightData>();
            var res = client.LocalHueClient.GetLightsAsync().Result;
            var ld = res.Select(r => new LightData(r)).ToList();
            var output = new List<LightData>();
            foreach (var light in ld) {
                foreach (var ex in lights.Where(ex => ex.Id == light.Id)) {
                    light.TargetSector = ex.TargetSector;
                    light.Brightness = ex.Brightness;
                    light.OverrideBrightness = ex.OverrideBrightness;
                }

                output.Add(light);
            }

            return output;
        }
    }
}