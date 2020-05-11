using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HueDream.Models.Util;
using Newtonsoft.Json;
using Q42.HueApi.Streaming;
using Q42.HueApi.Streaming.Models;

namespace HueDream.Models.StreamingDevice.Hue {
    public static class StreamingSetup {
        
        public static async Task StopStream(StreamingHueClient client, BridgeData b) {
            if (client == null || b == null) {
                throw new ArgumentException("Invalid argument.");
            }

            var id = b.SelectedGroup;
            LogUtil.Write("Stopping stream.");
            await client.LocalHueClient.SetStreamingAsync(id, false).ConfigureAwait(true);
            LogUtil.Write("Stopped.");
        }

        public static async Task<StreamingGroup> SetupAndReturnGroup(StreamingHueClient client, BridgeData b,
            CancellationToken ct) {
            if (client == null || b == null) {
                throw new ArgumentException("Invalid argument.");
            }

            var groupId = b.SelectedGroup;
            LogUtil.Write($"Created client, selecting group {groupId}.");
            //Get the entertainment group
            var group = client.LocalHueClient.GetGroupAsync(groupId).Result;
            if (group == null) {
                LogUtil.Write("Group is null, defaulting to first group...");
                var groups = b.GetGroups();
                if (groups.Count > 0) {
                    groupId = groups[0].Id;
                    group = client.LocalHueClient.GetGroupAsync(groupId).Result;
                    if (group != null) {
                        LogUtil.Write(@$"Selected first group: {groupId}");
                    } else {
                        LogUtil.Write(@"Unable to load group, can't connect for streaming.");
                        return null;
                    }
                }
            }

            //Create a streaming group
            if (group != null) {
                var lights = group.Lights;
                var mappedLights =
                    (from light in lights
                        from ml in b.Lights
                        where ml.Id == light && ml.TargetSector != -1
                        select light)
                    .ToList();
                var stream = new StreamingGroup(mappedLights);
                //Connect to the streaming group
                try {
                    await client.Connect(group.Id).ConfigureAwait(true);
                } catch (Exception e) {
                    LogUtil.Write(@"Exception: " + e);
                }

                //Start auto updating this entertainment group
#pragma warning disable 4014
                client.AutoUpdate(stream, ct);
#pragma warning restore 4014

                //Optional: Check if streaming is currently active
                var bridgeInfo = await client.LocalHueClient.GetBridgeAsync().ConfigureAwait(true);
                LogUtil.Write(bridgeInfo != null && bridgeInfo.IsStreamingActive ? @"Streaming is active." : @"Streaming is not active.");
                return stream;
            }

            return null;
        }
    }
}