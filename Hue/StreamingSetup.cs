using HueDream.HueDream;
using JsonFlatFileDataStore;
using Newtonsoft.Json;
using Q42.HueApi;
using Q42.HueApi.Models.Groups;
using Q42.HueApi.Streaming;
using Q42.HueApi.Streaming.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HueDream.Hue {
    public class StreamingSetup {

        public static async Task<StreamingGroup> SetupAndReturnGroup(List<string> lights, CancellationToken ct) {
            Console.WriteLine("Loading data from store?");
            DataStore store = DreamData.getStore();
            string hueIp = store.GetItem("hueIp");
            string hueUser = store.GetItem("hueUser");
            string hueKey = store.GetItem("hueKey");
            Console.WriteLine("Got hue keys.");
            Group entGroup = store.GetItem<Group>("entertainmentGroup");
            Console.WriteLine("Got group?");
            store.Dispose();
            Console.WriteLine("Disposed");
            Console.WriteLine("Creating client....");
            //Initialize streaming client
            StreamingHueClient client = new StreamingHueClient(hueIp, hueUser, hueKey);
            Console.WriteLine("Created client");
            //Get the entertainment group


            Group group = null;
            if (entGroup != null) {
                group = entGroup;
            } else {
                IReadOnlyList<Group> all = await client.LocalHueClient.GetEntertainmentGroups();
                Console.WriteLine("Got Groups");
                foreach (Group eg in all) {
                    bool valid = true;
                    Console.WriteLine("Comparing LightGroup: " + JsonConvert.SerializeObject(eg.Lights));
                    foreach (string s in lights) {
                        if (!eg.Lights.Contains(s)) {
                            valid = false;
                        }
                        if (valid) {
                            group = eg;
                        }
                    }
                }
            }

            if (group == null) {
                throw new HueException("No Entertainment Group found. Create one using the Hue App and link it in the web UI.");
            } else {
                Console.WriteLine($"Using Entertainment Group {group.Id}");
            }

            //Create a streaming group
            StreamingGroup stream = new StreamingGroup(group.Locations);
            Console.WriteLine("Group setup complete, connecting to client...");
            //Connect to the streaming group
            await client.Connect(group.Id);

            //Start auto updating this entertainment group
            client.AutoUpdate(stream, ct, 50, onlySendDirtyStates: false);

            //Optional: Check if streaming is currently active
            Bridge bridgeInfo = await client.LocalHueClient.GetBridgeAsync();
            Console.WriteLine(bridgeInfo.IsStreamingActive ? "Streaming is active" : "Streaming is not active");
            return stream;
        }
    }
}
