using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using HueDream.Models.Util;
using Q42.HueApi.Streaming;
using Q42.HueApi.Streaming.Models;

namespace HueDream.Models.StreamingDevice.Hue {
    public static class StreamingSetup {
        public static async Task StopStream(StreamingHueClient client, BridgeData b) {
            if (client == null || b == null) {
                throw new ArgumentException("Invalid argument.");
            }

            var id = b.SelectedGroup;
            //LogUtil.Write("Stopping stream...");
            await client.LocalHueClient.SetStreamingAsync(id, false).ConfigureAwait(true);
            //LogUtil.Write("Stopped.");
        }

        public static async Task<StreamingGroup> SetupAndReturnGroup(StreamingHueClient client, BridgeData b,
            CancellationToken ct) {
            
            if (client == null || b == null) {
                throw new ArgumentException("Invalid argument.");
            }

            try {
                var groupId = b.SelectedGroup;
                if (groupId == null && b.Groups != null && b.Groups.Count > 0) {
                    groupId = b.Groups[0].Id;
                }

                if (groupId == null) return null;
                //Get the entertainment group
                var group = client.LocalHueClient.GetGroupAsync(groupId).Result;
                if (group == null) {
                    LogUtil.Write("Group is null, defaulting to first group...");
                    var groups = b.Groups;
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
                    var mappedLights = new List<string>();
                    foreach (var light in lights) {
                        foreach (var ml in b.Lights) {
                            if (ml.Id == light && ml.TargetSector != -1) {
                                mappedLights.Add(light);
                            }
                        }
                    }

                    var stream = new StreamingGroup(mappedLights);
                    //Connect to the streaming group
                    try {
                        await client.Connect(group.Id);
                    } catch (SocketException e) {
                        LogUtil.Write(@"Exception: " + e.Message);
                    } catch (InvalidOperationException f) {
                        LogUtil.Write("Exception: " + f.Message);
                    }

                    //Start auto updating this entertainment group
#pragma warning disable 4014
                    client.AutoUpdate(stream, ct);
#pragma warning restore 4014

                    return stream;
                }
            } catch (SocketException e) {
                LogUtil.Write("Socket exception occurred, can't return group right now: " + e.Message);
            }

            return null;
        }
    }
}