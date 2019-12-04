using System;
using System.Threading;
using System.Threading.Tasks;
using HueDream.HueDream;
using Q42.HueApi;
using Q42.HueApi.Models.Groups;
using Q42.HueApi.Streaming;
using Q42.HueApi.Streaming.Models;

namespace HueDream.Hue {
    public static class StreamingSetup {
        private const string V = "Hue: Streaming is active.";
        private const string Value = "Hue: Group setup complete, connecting to client...";

        public static async Task<HueResults> StopStream() {
            var store = DreamData.GetStore();
            string hueIp = store.GetItem("hueIp");
            Console.WriteLine($@"Hue: Stopping stream at {hueIp}.");
            string hueUser = store.GetItem("hueUser");
            string hueKey = store.GetItem("hueKey");
            var entGroup = store.GetItem<Group>("entertainmentGroup");
            store.Dispose();
            Console.WriteLine($@"Hue: Creating client at {hueIp}...");
            //Initialize streaming client
            var client = new LocalHueClient(hueIp, hueUser, hueKey);
            var id = entGroup.Id;
            return await client.SetStreamingAsync(id, false).ConfigureAwait(true);
        }

        public static async Task<StreamingGroup> SetupAndReturnGroup(CancellationToken ct) {
            var store = DreamData.GetStore();
            string hueIp = store.GetItem("hueIp");
            string hueUser = store.GetItem("hueUser");
            string hueKey = store.GetItem("hueKey");
            var group = store.GetItem<Group>("entertainmentGroup");
            store.Dispose();
            Console.WriteLine(@"Hue: Creating client...");
            //Initialize streaming client
            var client = new StreamingHueClient(hueIp, hueUser, hueKey);
            Console.WriteLine(@"Hue: Created client.");
            //Get the entertainment group
            
            if (group == null) {
                throw new HueException("No Entertainment Group found. Create one using the Hue App and link it in the web UI.");
            }

            Console.WriteLine($@"Hue: Using Entertainment Group {group.Id}");

            //Create a streaming group
            var stream = new StreamingGroup(group.Locations);
            Console.WriteLine(Value);
            //Connect to the streaming group
            await client.Connect(group.Id).ConfigureAwait(true);

            //Start auto updating this entertainment group
            await client.AutoUpdate(stream, ct).ConfigureAwait(false);

            //Optional: Check if streaming is currently active
            var bridgeInfo = await client.LocalHueClient.GetBridgeAsync().ConfigureAwait(true);
            Console.WriteLine(bridgeInfo.IsStreamingActive ? V : "Hue: Streaming is not active.");
            return stream;
        }
    }
}