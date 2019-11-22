using Newtonsoft.Json;
using Q42.HueApi;
using Q42.HueApi.Models.Groups;
using Q42.HueApi.Streaming;
using Q42.HueApi.Streaming.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HueDream.HueControl {
    public class StreamingSetup {
               
        public static async Task<StreamingGroup> SetupAndReturnGroup(string ip, string key, string entertainmentKey, List<string> lights, CancellationToken ct) {

            var useSimulator = false;
            Console.WriteLine("Creating client....");
            //Initialize streaming client
            StreamingHueClient client = new StreamingHueClient(ip, key, entertainmentKey);
            Console.WriteLine("Created client");
            //Get the entertainment group
            var all = await client.LocalHueClient.GetEntertainmentGroups();
            Console.WriteLine("Got Groups");

            Group group = null;
            foreach (Group eg in all) {
                bool valid = true;
                Console.WriteLine("Comparing LightGroup: " + JsonConvert.SerializeObject(eg.Lights));
                foreach (string s in lights) {
                    if (!eg.Lights.Contains(s)) {
                        valid = false;
                    }
                    if (valid) group = eg;
                }
            }

            if (group == null) {
                throw new HueException("No Entertainment Group found. Create one using the Q42.HueApi.UniversalWindows.Sample");
            } else {
                Console.WriteLine($"Using Entertainment Group {group.Id}");
            }

            //Create a streaming group
            var stream = new StreamingGroup(group.Locations);
            stream.IsForSimulator = useSimulator;


            //Connect to the streaming group
            await client.Connect(group.Id, simulator: useSimulator);

            //Start auto updating this entertainment group
            client.AutoUpdate(stream, ct, 50, onlySendDirtyStates: false);

            //Optional: Check if streaming is currently active
            var bridgeInfo = await client.LocalHueClient.GetBridgeAsync();
            Console.WriteLine(bridgeInfo.IsStreamingActive ? "Streaming is active" : "Streaming is not active");
            return stream;
        }
    }
}
