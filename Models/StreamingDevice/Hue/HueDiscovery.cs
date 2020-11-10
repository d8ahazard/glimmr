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

namespace HueDream.Models.StreamingDevice.Hue {
    public static class HueDiscovery {
        
        public static async Task<List<BridgeData>> Refresh(CancellationToken ct) {
            var output = new List<BridgeData>();
            var foo = Task.Run(() => Discover(), ct);
            var newBridges = await foo;
            LogUtil.Write("New bridges: " + JsonConvert.SerializeObject(newBridges));
            var current = DataUtil.GetCollection<BridgeData>("Dev_Hue");
            foreach (var nb in newBridges) {
                foreach (var ex in current) {
                    if (ex.Id == nb.Id) {
                        nb.CopyBridgeData(ex);
                    }
                }
                output.Add(nb);
            }

            foreach (var ex in current) {
                var matched = false;
                foreach (var o in output.Where(o => o.Id == ex.Id)) {
                    matched = true;
                }
                if (!matched) output.Add(ex);
            }

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

        public static async Task<List<BridgeData>> Discover(int time = 5) {
            LogUtil.Write("Hue: Discovery Started.");
            var output = new List<BridgeData>();
            try {
                var discovered = await HueBridgeDiscovery.CompleteDiscoveryAsync(TimeSpan.FromSeconds(time),TimeSpan.FromSeconds(time));
                output = discovered.Select(bridge => new BridgeData(bridge)).ToList();
                LogUtil.Write($"Hue: Discovery complete, found {discovered.Count} devices.");
            } catch (TaskCanceledException e) {
                LogUtil.Write("Discovery exception, task canceled: " + e.Message, "WARN");
            } catch (OperationCanceledException e) {
                LogUtil.Write("Discovery exception, operation canceled: " + e.Message, "WARN");
            }catch (SocketException f) {
                LogUtil.Write("Socket exception, task canceled: " + f.Message, "WARN");
            } catch (HttpRequestException g) {
                LogUtil.Write("HTTP exception, task canceled: " + g.Message, "WARN");
            }

            return output;
        }


    }
}