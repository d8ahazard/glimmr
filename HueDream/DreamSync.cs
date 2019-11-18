using HueDream.DreamScreenControl;
using HueDream.HueControl;
using System;

namespace HueDream.HueDream {
    public class DreamSync {

        private HueBridge hueBridge;
        private DreamScreen dreamScreen;
        private DreamData dreamData;
        System.Threading.Thread sender;
        public bool doSync { get; set; }
        public DreamSync() {
            dreamData = new DreamData();
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
                dreamScreen.StartListening();
                hueBridge.startEntertainment();
                sender = new System.Threading.Thread(SyncData);
                sender.IsBackground = true;
                sender.Start();
                
            }
        }

        public void StopSync() {
            Console.WriteLine("Stopping sync.");
            doSync = false;            
            dreamScreen.StopListening();
            hueBridge.stopEntertainment();
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
