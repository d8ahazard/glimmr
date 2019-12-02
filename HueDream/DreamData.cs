using HueDream.DreamScreen.Devices;
using HueDream.Hue;
using JsonFlatFileDataStore;
using Newtonsoft.Json;
using Q42.HueApi.Models.Groups;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace HueDream.HueDream {
    [Serializable]
    public static class DreamData {
        public static DataStore getStore() {
            string path = GetConfigPath("store.json");
            bool createDefaults = false;
            if (!File.Exists(path)) {
                createDefaults = true;
            }

            DataStore store = new DataStore(path);
            if (createDefaults) {
                store.InsertItemAsync("dsIp", "0.0.0.0");
                BaseDevice myDevice = new SideKick(GetLocalIPAddress());
                myDevice.Initialize();
                store.InsertItemAsync("myDevice", myDevice);
                store.InsertItemAsync("emuType", "SideKick");
                store.InsertItemAsync("hueIp", HueBridge.findBridge());
                store.InsertItemAsync("hueSync", false);
                store.InsertItemAsync("hueAuth", false);
                store.InsertItemAsync("hueKey", "");
                store.InsertItemAsync("hueUser", "");
                store.InsertItemAsync("hueLights", new List<KeyValuePair<int, string>>());
                store.InsertItemAsync("hueMap", new List<LightMap>());
                store.InsertItemAsync("entertainmentGroups", new List<Group>());
                //store.InsertItemAsync<Group>("entertainmentGroup", null);
                store.InsertItemAsync("devices", Array.Empty<BaseDevice>());
            }
            return store;
        }
        /// <summary>
        /// Loads our datastore from a dynamic path, and tries to get the item
        /// </summary>
        /// <param name="key"></param>
        /// <returns>dynamic object corresponding to key, or null if not found</returns>
        public static dynamic GetItem(string key) {
            try {
                using (DataStore dStore = new DataStore(GetConfigPath("store.json"))) {
                    dynamic output = dStore.GetItem(key);
                    return output;
                }
            } catch (KeyNotFoundException) {

            }
            return null;
        }

        public static dynamic GetItem<T>(string key) {
            try {
                using (DataStore dStore = new DataStore(GetConfigPath("store.json"))) {
                    dynamic output = dStore.GetItem<T>(key);
                    return output;
                }
            } catch (KeyNotFoundException) {

            }
            return null;
        }

        public static bool SetItem(string key, dynamic value) {
            using (DataStore dStore = new DataStore(GetConfigPath("store.json"))) {

                dynamic output = dStore.ReplaceItem(key, value, true);
                return output;
            }
        }

        public static bool SetItem<T>(string key, dynamic value) {
            using (DataStore dStore = new DataStore(GetConfigPath("store.json"))) {

                dynamic output = dStore.ReplaceItem<T>(key, value, true);
                return output;
            }
        }

        public static DataObj GetStoreSerialized() {
            string jsonPath = GetConfigPath("store.json");
            if (File.Exists(jsonPath)) {
                try {
                    using (StreamReader file = File.OpenText(jsonPath)) {
                        JsonSerializer jss = new JsonSerializer();
                        return (DataObj)jss.Deserialize(file, typeof(DataObj));
                    }
                } catch (IOException e) {
                    Console.WriteLine("An IO Exception occurred: " + e.ToString());
                }
            }
            return null;
        }

        public static BaseDevice GetDeviceData() {
            using DataStore dd = getStore();
            BaseDevice dev;
            string devType = dd.GetItem("emuType");
            if (devType == "SideKick") {
                dev = dd.GetItem<SideKick>("myDevice");
            } else {
                dev = dd.GetItem<Connect>("myDevice");
            }
            return dev;
        }

        /// <summary>
        /// Determine if config path is local, or docker
        /// </summary>
        /// <param name="filePath">Config file to check</param>
        /// <returns>Modified path to config file</returns>
        private static string GetConfigPath(string filePath) {
            if (Directory.Exists("/etc/huedream")) {
                if (File.Exists(filePath)) {
                    Console.WriteLine("We should move our ini to /etc");
                    File.Copy(filePath, "/etc/huedream/" + filePath);
                    if (File.Exists("/etc/huedream/huedream.ini")) {
                        Console.WriteLine("File moved, updating INI path.");
                        File.Delete(filePath);
                    }
                }
                return "/etc/huedream/" + filePath;
            }
            return filePath;
        }

        private static string GetLocalIPAddress() {
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList) {
                if (ip.AddressFamily == AddressFamily.InterNetwork) {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }
    }
}
