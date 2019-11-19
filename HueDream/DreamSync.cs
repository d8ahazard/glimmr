using HueDream.DreamScreenControl;
using HueDream.HueControl;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HueDream.HueDream {
    public class DreamSync {

        private HueBridge hueBridge;
        private DreamScreen dreamScreen;
        private DreamData dreamData;
        
        private CancellationTokenSource syncToken;
        
        public static bool doSync { get; set; }
        public DreamSync() {
            dreamData = new DreamData();
            Console.WriteLine("Creating new sync?");
            syncToken = new CancellationTokenSource();
            if (dreamData.DS_IP != "0.0.0.0") {
                hueBridge = new HueBridge();
                dreamScreen = new DreamScreen(dreamData.DS_IP);
            }
        }

        

        public void startSync() {
            if (doSync) {
                Console.WriteLine("Sync is already started...");
            } else {
                Console.WriteLine("Starting sync.");
                doSync = true;
                DreamScreen.listening = true;
                dreamScreen.Listen(syncToken.Token);                
                Task.Run(async() => hueBridge.StartStream());
                Task.Run(async() => SyncData(syncToken.Token), syncToken.Token);
                
            }
        }

        public void StopSync() {
            Console.WriteLine("Dreamsync: Stopsync fired.");
            doSync = false;
            hueBridge.StopStream();
            DreamScreen.listening = false;
            syncToken.Cancel();            
        }

        private void SyncData(CancellationToken token) {
            Console.WriteLine("SyncData called");            
            while (!token.IsCancellationRequested) {
                // Read updating color var from dreamscreen
                hueBridge.setColors(dreamScreen.colors);
            }
            Console.WriteLine("Dosync completed.");
        }
    }
}
