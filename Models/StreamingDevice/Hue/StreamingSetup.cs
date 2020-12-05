using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Newtonsoft.Json;
using Q42.HueApi.Streaming;
using Q42.HueApi.Streaming.Models;

namespace Glimmr.Models.StreamingDevice.Hue {
    public static class StreamingSetup {
        public static async Task StopStream(StreamingHueClient client, BridgeData b) {
            if (client == null || b == null) {
                throw new ArgumentException("Invalid argument.");
            }

            var id = b.SelectedGroup;
            await client.LocalHueClient.SetStreamingAsync(id, false).ConfigureAwait(true);
        }

        public static async Task<StreamingGroup> SetupAndReturnGroup(StreamingHueClient client, BridgeData b,
            CancellationToken ct) {
            
            if (client == null || b == null) {
                throw new ArgumentException("Invalid argument.");
            }
            try {
                var groupId = b.SelectedGroup;
                LogUtil.Write("HueStream: Selecting group ID: " + groupId);
                if (groupId == null && b.Groups != null && b.Groups.Count > 0) {
                    groupId = b.Groups[0].Id;
                }

                if (groupId == null) {
                    LogUtil.Write("HueStream: Group ID is null!");
                    return null;
                }
                //Get the entertainment group
                LogUtil.Write("Grabbing ent group...");
                var group = client.LocalHueClient.GetGroupAsync(groupId).Result;
                if (group == null) {
                    LogUtil.Write("Group is null, trying with first group ID.");
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
                } else {
                    LogUtil.Write("HueStream: Entertainment Group retrieved...");
                }

                //Create a streaming group
                if (group != null) {
                    var lights = group.Lights;
                    LogUtil.Write("HueStream: We have group, mapping lights: " + JsonConvert.SerializeObject(lights));
                    var mappedLights = new List<string>();
                    foreach (var light in lights) {
                        foreach (var ml in b.Lights) {
                            if (ml.Id == light && ml.TargetSector != -1 || ml.TargetSectorV2 != -1) {
                                LogUtil.Write("Adding mapped ID: " + ml.Id);
                                mappedLights.Add(light);
                            }
                        }
                    }

                    LogUtil.Write("Getting streaming group using ml: " + JsonConvert.SerializeObject(mappedLights));
                    var stream = new StreamingGroup(mappedLights);
                    LogUtil.Write("Stream Got.");
                    //Connect to the streaming group
                    try {
                        LogUtil.Write("Connecting...");
                        await client.Connect(group.Id);
                        LogUtil.Write("Connected.");
                    } catch (SocketException e) {
                        LogUtil.Write(@"Exception: " + e.Message);
                    } catch (InvalidOperationException f) {
                        LogUtil.Write("Exception: " + f.Message);
                    } catch (Exception) {
                        LogUtil.Write("Random exception caught.");
                    }

                    //Start auto updating this entertainment group
#pragma warning disable 4014
                    client.AutoUpdate(stream, ct);
#pragma warning restore 4014

                    return stream;
                }

                LogUtil.Write("Uh, the group retrieved from teh thinger is null...");

            } catch (SocketException e) {
                LogUtil.Write("Socket exception occurred, can't return group right now: " + e.Message);
            }

            return null;
        }
    }
}