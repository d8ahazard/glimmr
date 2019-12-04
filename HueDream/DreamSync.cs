using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HueDream.DreamScreen;
using HueDream.Hue;
using Q42.HueApi.Models.Groups;

namespace HueDream.HueDream {
    public sealed class DreamSync : IDisposable {
        private readonly DreamClient dreamScreen;

        private readonly HueBridge hueBridge;
        private bool disposed;
        private CancellationTokenSource syncTokenSource;

        public DreamSync() {
            var store = DreamData.GetStore();
            store.Dispose();
            string dsIp = DreamData.GetItem("dsIp");

            Console.WriteLine($@"DreamSync: Creating new sync at {dsIp}...");
            hueBridge = new HueBridge();
            dreamScreen = new DreamClient(this);
            // Start our dream screen listening like a good boy
            if (!DreamClient.Listening) {
                Console.WriteLine($@"DreamSync:  Listen started at {dsIp}...");
                dreamScreen.Listen().ConfigureAwait(false);
                // Start another listener for show state change
                DreamClient.Listening = true;
            }

            if (dsIp == "0.0.0.0") dreamScreen.FindDevices().ConfigureAwait(false);
        }

        private static bool SyncEnabled { get; set; }


        public void Dispose() {
            Dispose(true);
            //GC.SuppressFinalize(this);
        }


        private void StartSync() {
            Console.WriteLine(@"DreamSync: Starting sync...");
            syncTokenSource = new CancellationTokenSource();
            dreamScreen.Subscribe();
            Task.Run(async () => await SyncData(syncTokenSource.Token).ConfigureAwait(false));
            Task.Run(async () => await hueBridge.StartStream(syncTokenSource.Token).ConfigureAwait(false));
            Task.Run(async () => await dreamScreen.CheckShow(syncTokenSource.Token).ConfigureAwait(false));
            Console.WriteLine(@"DreamSync: Sync running.");
            SyncEnabled = true;
        }

        private void StopSync() {
            Console.WriteLine($@"DreamSync: Stopping Sync...{SyncEnabled}");
            syncTokenSource.Cancel();
            hueBridge.StopEntertainment();
            SyncEnabled = false;
            Console.WriteLine($@"DreamSync: Sync Stopped. {SyncEnabled}");
        }

        private async Task SyncData(CancellationToken ct) {
            await Task.Run(() => {
                while (!ct.IsCancellationRequested) {
                    hueBridge.SetColors(dreamScreen.Colors);
                    hueBridge.Brightness = dreamScreen.Brightness;
                    hueBridge.DreamSceneBase = dreamScreen.SceneBase;
                }
            }).ConfigureAwait(true);
        }

        public void CheckSync(bool enabled) {
            if (CanSync()) {
                if (enabled && !SyncEnabled)
                    Task.Run(StartSync);
                else if (!enabled && SyncEnabled) StopSync();
            }
        }

        private static bool CanSync() {
            var store = DreamData.GetStore();
            string dsIp = store.GetItem("dsIp");
            bool hueAuth = store.GetItem("hueAuth");
            var map = store.GetItem<List<LightMap>>("hueMap");
            store.Dispose();
            Group entGroup = DreamData.GetItem<Group>("entertainmentGroup");
            if (dsIp != "0.0.0.0") {
                if (hueAuth) {
                    if (entGroup != null) {
                        if (map.Count > 0) return true;

                        Console.WriteLine(@"No lights mapped.");
                    }
                    else {
                        Console.WriteLine(@"No entertainment group.");
                    }
                }
                else {
                    Console.WriteLine(@"Hue is not authorized.");
                }
            }
            else {
                Console.WriteLine(@"No target IP.");
            }

            return false;
        }

        // Protected implementation of Dispose pattern.
        private void Dispose(bool disposing) {
            if (disposed) return;

            if (disposing) {
                if (syncTokenSource != null) {
                    //syncTokenSource.Dispose();
                    //dreamTokenSource.Dispose();
                }

                dreamScreen.Dispose();
            }

            disposed = true;
        }
    }
}