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
                    doSync = true;
                    sender = new System.Threading.Thread(SyncData);
                    sender.IsBackground = true;
                    sender.Start();
                }
            }
        }

        public void StopSync() {
            doSync = false;
            if (sender.Join(200) == false) {
                sender.Abort();
            }
            sender = null;
        }

        private void SyncData() {
            Console.WriteLine("SyncData fired.");
            dreamScreen.startListening();
            Task bill = hueBridge.startEntertainment();
            while (doSync) {
                // Read updating color var from dreamscreen
                var colors = dreamScreen.colors;
                // Save it to the hue bridge's variable
                hueBridge.colors = colors;
            }
            

        }
    }
}
