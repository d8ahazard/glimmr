using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
        
        public static async Task<List<BridgeData>> Refresh(CancellationToken ct) {
            var output = new List<BridgeData>();
            var foo = Task.Run(() => Discover(), ct);
            var newBridges = await foo;
            foreach (var nb in newBridges) {
                var staticData = nb;
                var ex = DataUtil.GetCollectionItem<BridgeData>("bridges", nb.Id);
                LogUtil.Write("Looping for bridge...");
                if (ex != null) nb.CopyBridgeData(ex);
                if (nb.Key != null && nb.User != null) {
                    try {
                        LogUtil.Write($"Refreshing bridge: {nb.Id} - {nb.IpAddress}");
                        var nhb = new HueBridge(nb);
                        nhb.RefreshData();
                        staticData = nhb.Bd;
                    } catch (Exception e) {
                        LogUtil.Write("Socket Exception: " + e.Message, "ERROR");
                    }
                }
                LogUtil.Write("Adding bridge to output.");
                output.Add(staticData);
                DataUtil.InsertCollection<BridgeData>("bridges",staticData);
                LogUtil.Write("ADDED.");
            }
            LogUtil.Write("Setting the damned list of bridges...");
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


    }
}