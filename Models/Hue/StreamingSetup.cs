using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Q42.HueApi;
using Q42.HueApi.Models.Groups;
using Q42.HueApi.Streaming;
using Q42.HueApi.Streaming.Models;

namespace HueDream.Models.Hue {
    public static class StreamingSetup {
        private const string V = "Hue: Streaming is active.";
        private const string Value = "Hue: Group setup complete, connecting to client...";

        public static async Task<HueResults> StopStream(BridgeData b) {
            var store = DreamData.GetStore();
            string hueIp = b.Ip;
            Console.WriteLine($@"Hue: Stopping stream at {hueIp}.");
            string hueUser = b.User;
            string hueKey = b.Key;
            string id = b.SelectedGroup;
            store.Dispose();
            Console.WriteLine($@"Hue: Creating client at {hueIp}...");
            //Initialize streaming client
            var client = new LocalHueClient(hueIp, hueUser, hueKey);
            return await client.SetStreamingAsync(id, false).ConfigureAwait(true);
        }

        public static async Task<StreamingGroup> SetupAndReturnGroup(BridgeData b, CancellationToken ct) {
            string hueIp = b.Ip;
            string hueUser = b.User;
            string hueKey = b.Key;
            string groupId = b.SelectedGroup;
            Console.WriteLine(@"Hue: Creating client...");
            //Initialize streaming client
            var client = new StreamingHueClient(hueIp, hueUser, hueKey);
            Console.WriteLine(@"Hue: Created client.");
            //Get the entertainment group
            var group = client.LocalHueClient.GetGroupAsync(groupId).Result;
            Console.WriteLine(@"Group fetched from " + groupId);
            //Create a streaming group
            var lights = group.Lights;
            Console.WriteLine(@"Group Lights: " + JsonConvert.SerializeObject(lights));
            var mappedLights = (from light in lights from ml in b.Lights where ml.Id == light && ml.TargetSector != -1 select light).ToList();
            Console.WriteLine(@"Using mapped lights for group: " + JsonConvert.SerializeObject(mappedLights));
            var stream = new StreamingGroup(mappedLights);
            Console.WriteLine(Value);
            //Connect to the streaming group
            await client.Connect(group.Id).ConfigureAwait(true);

            //Start auto updating this entertainment group
#pragma warning disable 4014
            client.AutoUpdate(stream, ct,30, true);
#pragma warning restore 4014

            //Optional: Check if streaming is currently active
            var bridgeInfo = await client.LocalHueClient.GetBridgeAsync().ConfigureAwait(true);
            Console.WriteLine(bridgeInfo.IsStreamingActive ? V : "Hue: Streaming is not active.");
            return stream;
        }
    }
}