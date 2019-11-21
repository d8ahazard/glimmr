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
            hueBridge = new HueBridge();
            dreamScreen = new DreamScreen();
            // Start our dreamscreen listening like a good boy
            if (!DreamScreen.listening) {
                Console.WriteLine("Calling listen on dreamscreen.");
                dreamScreen.Listen();
                DreamScreen.listening = true;
                Console.WriteLine("Listen called.");
            }
        }

        

        public void startSync() {
            if (doSync) {
                Console.WriteLine("Sync is already started...");
            } else {
                Console.WriteLine("Starting sync.");
                doSync = true;
                DreamScreen.subscribed = true;
                Task.Run(async() => hueBridge.StartStream());
                Task.Run(async() => SyncData());                
            }
        }

        public void StopSync() {
            Console.WriteLine("Dreamsync: Stopsync fired.");
            doSync = false;
            hueBridge.StopStream();
            DreamScreen.subscribed = false;
            syncToken.Cancel();            
        }

        private void SyncData() {
            Console.WriteLine("SyncData called");            
            while (doSync) {
                // Read updating color var from dreamscreen
                hueBridge.setColors(dreamScreen.colors);
            }
            Console.WriteLine("Dosync completed.");
        }
    }
}
