using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using HueDream.Models.Util;
using Newtonsoft.Json;
using Q42.HueApi;
using Q42.HueApi.Interfaces;
using Q42.HueApi.Models.Bridge;
using Q42.HueApi.Models.Groups;
using Q42.HueApi.Streaming;

namespace HueDream.Models.StreamingDevice.Hue {
    public static class HueDiscovery {
        private static StreamingHueClient _hc;

        private static async Task<List<Group>> ListGroups() {
            var all = await _hc.LocalHueClient.GetEntertainmentGroups();
            var output = new List<Group>();
            output.AddRange(all);
            LogUtil.Write("Listed.");
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

        public static async Task<List<BridgeData>> Refresh(CancellationToken ct) {
            var output = new List<BridgeData>();
            var foo = Task.Run(() => Discover(), ct);
            var newBridges = await foo;
            foreach (var nb in newBridges) {
                var ex = DataUtil.GetCollectionItem<BridgeData>("bridges", nb.Id);
                LogUtil.Write("Looping for bridge...");
                if (ex != null) nb.CopyBridgeData(ex);
                if (nb.Key != null && nb.User != null) {
                    try {
                        _hc?.Dispose();
                    } catch (ObjectDisposedException) {
                        LogUtil.Write("Client is already disposed...");
                    }

                    _hc = new StreamingHueClient(nb.IpAddress, nb.User, nb.Key);
                    try {
                        LogUtil.Write($"Refreshing bridge: {nb.Id} - {nb.IpAddress}");
                        nb.Lights = GetLights(nb);
                        nb.Groups = ListGroups().Result;
                    } catch (Exception e) {
                        LogUtil.Write("Socket Exception: " + e.Message, "ERROR");
                    }
                }
                LogUtil.Write("Adding bridge to output.");
                output.Add(nb);
                DataUtil.InsertCollection<BridgeData>("bridges",nb);
                LogUtil.Write("ADDED.");
                LogUtil.Write("Disposing client again.");
                _hc?.Dispose();
                LogUtil.Write("Disposed client.");
            }
            LogUtil.Write("Setting the damned list of bridges...");
            return output;
        }

        public static async Task<List<BridgeData>> Discover(int time = 5) {
            LogUtil.Write("Hue: Discovery Started.");
            var output = new List<BridgeData>();
            try {
                var discovered = await HueBridgeDiscovery.CompleteDiscoveryAsync(TimeSpan.FromSeconds(time),TimeSpan.FromSeconds(time));
                LogUtil.Write("Fast discovery done...");
                output = discovered.Select(bridge => new BridgeData(bridge)).ToList();
                LogUtil.Write($"Hue: Discovery complete, found {discovered.Count} devices.");
            } catch (TaskCanceledException e) {
                LogUtil.Write("Discovery exception, task canceled: " + e.Message, "WARN");
            } catch (SocketException f) {
                LogUtil.Write("Socket exception, task canceled: " + f.Message, "WARN");
            } catch (HttpRequestException g) {
                LogUtil.Write("HTTP exception, task canceled: " + g.Message, "WARN");
            }

            return output;
        }


        public static List<LightData> GetLights(BridgeData bd) {
            if (bd == null) throw new ArgumentException("Invalid argument.");
            // If we have no IP or we're not authorized, return
            if (bd.IpAddress == "0.0.0.0" || bd.User == null || bd.Key == null) return new List<LightData>();
            // Create client
            LogUtil.Write("Adding lights...");
            _hc.LocalHueClient.Initialize(bd.User);
            // Get lights
            var lights = bd.Lights ?? new List<LightData>();
            var res = _hc.LocalHueClient.GetLightsAsync().Result;
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
        
        public static List<LightData> GetLights(BridgeData bd, StreamingHueClient client) {
            if (bd == null || client == null) throw new ArgumentException("Invalid argument.");
            // If we have no IP or we're not authorized, return
            if (bd.IpAddress == "0.0.0.0" || bd.User == null || bd.Key == null) return new List<LightData>();
            // Create client
            LogUtil.Write("Adding lights...");
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
            LogUtil.Write("Lights: " + JsonConvert.SerializeObject(output));
            return output;
        }
    }
}