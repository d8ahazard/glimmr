using HueDream.DreamScreenControl;
using HueDream.HueControl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HueDream.HueDream {
    public class DreamSync {

        private HueBridge hueBridge;
        private DreamScreen dreamScreen;
        private DreamData dreamData;
        System.Threading.Thread sender;
        public bool doSync { get; set; }
        public string[][] colors { get; }
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
                if (dreamData.HUE_SYNC && dreamData.HUE_AUTH) {
                    Console.WriteLine("Starting sync.");
                    doSync = true;
                    sender = new System.Threading.Thread(SyncData);
                    sender.IsBackground = true;
                    sender.Start();
                }
            }
        }

        public void StopSync() {
            Console.WriteLine("Stopping sync.");
            doSync = false;
            if (sender.Join(200) == false) {
                sender.Abort();
            }
            sender = null;
        }

        private void SyncData() {
            dreamScreen.startListening();
            Task bill = hueBridge.startEntertainment();
            while (doSync) {
                // Read updating color var from dreamscreen
                hueBridge.colors = dreamScreen.colors;
            }
        }
    }
}
