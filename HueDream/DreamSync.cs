using HueDream.DreamScreen;
using HueDream.DreamScreen.Devices;
using HueDream.Hue;
using JsonFlatFileDataStore;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
            string dsIp = store.GetItem("dsIp");
            store.Dispose();
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
            if (dsIp == "0.0.0.0") {
                Console.WriteLine("Searching for DS Devices.");
                dreamScreen.FindDevices();
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
            }
        }

        public void CheckSync(bool enabled) {
            if (CanSync()) {
                if (enabled && !syncEnabled) {
                    Task.Run(() => startSync());
                } else if (!enabled && syncEnabled) {
                    StopSync();
                }
            }
        }

        private bool CanSync() {
            DataStore store = DreamData.getStore();
            string dsIp = store.GetItem("dsIp");
            bool hueAuth = store.GetItem("hueAuth");
            Group entGroup = store.GetItem<Group>("entertainmentGroup");
            List<LightMap> map = store.GetItem<List<LightMap>>("hueMap");
            store.Dispose();
            if (dsIp != "0.0.0.0") {
                if (hueAuth) {
                    if (entGroup.Id != null) {
                        if (map.Count > 0) {
                            return true;
                        } else {
                            Console.WriteLine("No lights mapped.");
                        }
                    } else {
                        Console.WriteLine("No entertainment group.");
                    }
                } else {
                    Console.WriteLine("Hue is not authorized.");
                }
            } else {
                Console.WriteLine("No target IP.");
            }
            return false;
        }
    }
}
