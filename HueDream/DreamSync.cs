using HueDream.DreamScreenControl;
using HueDream.HueControl;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace HueDream.HueDream {
    public class DreamSync {

        private HueBridge hueBridge;
        private DreamScreen dreamScreen;
        private DreamData dreamData;

        public static bool doSync { get; set; }
        public DreamSync() {
            Console.WriteLine("Creating new sync.");
            dreamData = new DreamData();
            Console.WriteLine("Data loaded.");
            hueBridge = new HueBridge(dreamData);
            Console.WriteLine("Bridge created.");
            dreamScreen = new DreamScreen(this, dreamData);
            Console.WriteLine("DS Create.");
            // Start our dreamscreen listening like a good boy
            if (!DreamScreen.listening) {
                Console.WriteLine("DS Listen start.");
                dreamScreen.Listen();
                DreamScreen.listening = true;
                Console.WriteLine("DS Listen running.");
            }
        }


        public void startSync() {
            if (doSync) {
                Console.WriteLine("Sync is already started...");
            } else {
                Console.WriteLine("Starting sync.");
                doSync = true;
                dreamScreen.subscribe();
                Task.Run(async() => hueBridge.StartStream());
                Task.Run(async() => SyncData());                
            }
        }

        public void StopSync() {
            Console.WriteLine("Dreamsync: Stopsync fired.");
            doSync = false;
            HueBridge.doEntertain = false;
        }

        private void SyncData() {
            hueBridge.setColors(dreamScreen.colors);            
        }

        public void CheckSync(bool enabled) {
            Console.WriteLine("DSSYNC: " + doSync + " HS: " + enabled);
            if (dreamData.DS_IP != "0.0.0.0" && enabled && !doSync) {
                Console.WriteLine("Beginning DS stream to Hue...");
                Task.Run(() => startSync());
            } else if (!enabled && doSync) {
                Console.WriteLine("Stopping sync.");
                StopSync();
            }
        }
    }
}
