using HueDream.DreamScreen;
using HueDream.DreamScreen.Devices;
using HueDream.Hue;
using JsonFlatFileDataStore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HueDream.HueDream {
    public class DreamSync {

        private HueBridge hueBridge;
        private DreamClient dreamScreen;
        private CancellationTokenSource cts;
        
        public static bool syncEnabled { get; set; }
        public DreamSync() {
            DataStore store = DreamData.getStore();
            store.Dispose();
            BaseDevice dev = DreamData.GetDeviceData();
            Console.WriteLine("DreamSync: Creating new sync...");
            hueBridge = new HueBridge();
            dreamScreen = new DreamClient(this);
            // Start our dreamscreen listening like a good boy
            if (!DreamClient.listening) {
                Console.WriteLine("DreamSync:  Listen start...");
                dreamScreen.Listen();
                DreamClient.listening = true;
                Console.WriteLine("DreamSync: Listening.");
            }
        }


        public void startSync() {
            Console.WriteLine("DreamSync: Starting sync...");
            cts = new CancellationTokenSource();
            dreamScreen.Subscribe();
            Task.Run(async () => SyncData(cts.Token));
            Task.Run(async () => hueBridge.StartStream(cts.Token, this));
            Console.WriteLine("DreamSync: Sync running.");
            syncEnabled = true;
        }

        public void StopSync() {
            Console.WriteLine("DreamSync: Stopping Sync...");
            cts.Cancel();
            hueBridge.StopEntertainment();
            syncEnabled = false;
            Console.WriteLine("DreamSync: Sync Stopped.");
        }

        private void SyncData(CancellationToken ct) {
            while (!ct.IsCancellationRequested) {
                hueBridge.SetColors(dreamScreen.colors);
                hueBridge.Brightness = dreamScreen.Brightness;
                //Console.WriteLine("SYNCLOOP: " + hueBridge.Brightness);
            }
            //hueBridge.SetSaturation(dreamScreen.Saturation);            
        }

        public void CheckSync(bool enabled) {
            if (DreamData.GetItem("dsIp") != "0.0.0.0" && enabled && !syncEnabled) {
                Task.Run(() => startSync());
            } else if (!enabled && syncEnabled) {
                StopSync();
            }
        }
    }
}
