using HueDream.DreamScreen;
using HueDream.Hue;
using JsonFlatFileDataStore;
using Q42.HueApi.Models.Groups;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HueDream.HueDream {
    public class DreamSync : IDisposable {

        private readonly HueBridge hueBridge;
        private readonly DreamClient dreamScreen;
        private CancellationTokenSource syncTokenSource;
        private readonly CancellationTokenSource dreamTokenSource;
        private bool disposed = false;

        public static bool syncEnabled { get; set; }
        public DreamSync() {
            dreamTokenSource = new CancellationTokenSource();
            DataStore store = DreamData.getStore();
            store.Dispose();
            string dsIp = DreamData.GetItem("dsIp");

            Console.WriteLine($"DreamSync: Creating new sync at {dsIp}...");
            hueBridge = new HueBridge();
            dreamScreen = new DreamClient(this);
            // Start our dreamscreen listening like a good boy
            if (!DreamClient.listening) {
                Console.WriteLine($"DreamSync:  Listen started at {dsIp}...");
                dreamScreen.Listen().ConfigureAwait(false);
                // Start another listener for show state change
                Task.Run(async () => await dreamScreen.CheckShow(dreamTokenSource.Token).ConfigureAwait(false));
                DreamClient.listening = true;
            }
            if (dsIp == "0.0.0.0") {
                dreamScreen.FindDevices().ConfigureAwait(false);
            }
        }


        public void startSync() {
            Console.WriteLine("DreamSync: Starting sync...");
            syncTokenSource = new CancellationTokenSource();
            dreamScreen.Subscribe();
            Task.Run(async () => await SyncData(syncTokenSource.Token).ConfigureAwait(false));
            Task.Run(async () => await hueBridge.StartStream(syncTokenSource.Token).ConfigureAwait(false));
            Console.WriteLine("DreamSync: Sync running.");
            syncEnabled = true;
        }

        public void StopSync() {
            Console.WriteLine($"DreamSync: Stopping Sync...{syncEnabled}");
            syncTokenSource.Cancel();
            hueBridge.StopEntertainment();
            syncEnabled = false;
            Console.WriteLine($"DreamSync: Sync Stopped. {syncEnabled}");
        }

        private async Task SyncData(CancellationToken ct) {
            await Task.Run(() => {
                while (!ct.IsCancellationRequested) {
                    hueBridge.SetColors(dreamScreen.colors);
                    hueBridge.Brightness = dreamScreen.Brightness;
                    hueBridge.DreamSceneBase = dreamScreen.sceneBase;
                }
            }).ConfigureAwait(true);
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

        private static bool CanSync() {
            DataStore store = DreamData.getStore();
            string dsIp = store.GetItem("dsIp");
            bool hueAuth = store.GetItem("hueAuth");
            List<LightMap> map = store.GetItem<List<LightMap>>("hueMap");
            store.Dispose();
            Group entGroup = DreamData.GetItem<Group>("entertainmentGroup");
            if (dsIp != "0.0.0.0") {
                if (hueAuth) {
                    if (entGroup != null) {
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


        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing) {
            if (disposed) {
                return;
            }

            if (disposing) {
                if (syncTokenSource != null) {
                    syncTokenSource.Dispose();
                    dreamTokenSource.Dispose();
                }
                dreamScreen.Dispose();
            }

            disposed = true;
        }
    }
}
