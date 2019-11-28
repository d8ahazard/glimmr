using HueDream.Hue;
using JsonFlatFileDataStore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HueDream.HueDream {
    public class DreamSync {

        private readonly HueBridge hueBridge;
        private readonly DreamScreen.DreamClient dreamScreen;
        private CancellationTokenSource cts;

        public static bool syncEnabled { get; set; }
        public DreamSync() {
            DataStore store = DreamData.getStore();
            store.Dispose();
            Console.WriteLine("Creating new sync.");
            hueBridge = new HueBridge();
            dreamScreen = new DreamScreen.DreamClient(this);
            // Start our dreamscreen listening like a good boy
            if (!DreamScreen.DreamClient.listening) {
                Console.WriteLine("DS Listen start.");
                dreamScreen.Listen();
                DreamScreen.DreamClient.listening = true;
                Console.WriteLine("DS Listen running.");
            }
        }


        public void startSync() {
            cts = new CancellationTokenSource();
            Console.WriteLine("Starting sync.");
            dreamScreen.Subscribe();
            Task.Run(async () => SyncData());
            Task.Run(async () => hueBridge.StartStream(cts.Token));
            Console.WriteLine("Sync should be running.");
            syncEnabled = true;
        }

        public void StopSync() {
            Console.WriteLine("Dreamsync: Stopsync fired.");
            cts.Cancel();
            hueBridge.StopEntertainment();
            syncEnabled = false;
        }

        private void SyncData() {
            hueBridge.setColors(dreamScreen.colors);
        }

        public void CheckSync(bool enabled) {
            if (DreamData.GetItem("dsIp") != "0.0.0.0" && enabled && !syncEnabled) {
                Console.WriteLine("Beginning DS stream to Hue...");
                Task.Run(() => startSync());
            } else if (!enabled && syncEnabled) {
                Console.WriteLine("Stopping sync.");
                StopSync();
            }
        }
    }
}
