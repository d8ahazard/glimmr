using HueDream.Hue;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HueDream.HueDream {
    public class DreamSync {

        private readonly HueBridge hueBridge;
        private readonly DreamScreen.DreamClient dreamScreen;
        private readonly DataObj dreamData;
        private CancellationTokenSource cts;
        private static long lastStopTime;

        public static bool syncEnabled { get; set; }
        public DreamSync() {
            Console.WriteLine("Creating new sync.");
            dreamData = DreamData.LoadJson();
            lastStopTime = 0;
            hueBridge = new HueBridge(dreamData);
            dreamScreen = new DreamScreen.DreamClient(this, dreamData);
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
            dreamScreen.subscribe();
            Task.Run(async () => SyncData());
            Task.Run(async () => hueBridge.StartStream(cts.Token));
            Console.WriteLine("Sync should be running.");
            syncEnabled = true;
        }

        public void StopSync() {
            Console.WriteLine("Dreamsync: Stopsync fired.");
            cts.Cancel();
            syncEnabled = false;
            lastStopTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

        private void SyncData() {
            hueBridge.setColors(dreamScreen.colors);
        }

        public void CheckSync(bool enabled) {
            if (dreamData.DsIp != "0.0.0.0" && enabled && !syncEnabled) {
                Console.WriteLine("Beginning DS stream to Hue...");
                if (DateTimeOffset.Now.ToUnixTimeMilliseconds() - lastStopTime > 10000.0) {
                    Task.Run(() => startSync());
                } else {
                    Console.WriteLine("Can't start, waiting for ent group to be open.");
                }
            } else if (!enabled && syncEnabled) {
                Console.WriteLine("Stopping sync.");
                StopSync();
            }
        }
    }
}
