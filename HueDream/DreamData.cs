using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using HueDream.DreamScreen.Devices;
using HueDream.Hue;
using JsonFlatFileDataStore;
using Newtonsoft.Json;
using Q42.HueApi;
using Q42.HueApi.Models.Bridge;
using Q42.HueApi.Models.Groups;

namespace HueDream.HueDream {
    [Serializable]
    public static class DreamData {
        public static DataStore GetStore() {
            var path = GetConfigPath("store.json");
            var createDefaults = !File.Exists(path);
            var store = new DataStore(path);
            UpgradeBridgeStorage();
            if (!createDefaults) return store;
            // Make our store if it doesn't already exist
            store.InsertItemAsync("dsIp", "0.0.0.0");
            BaseDevice myDevice = new SideKick(GetLocalIpAddress());
            myDevice.Initialize();
            var bList = HueBridge.FindBridges();
            var bData = new List<BridgeData>();
            foreach (LocatedBridge lb in bList) {
                bData.Add(new BridgeData(lb.IpAddress, lb.BridgeId));
            }
            store.InsertItemAsync("myDevice", myDevice);
            store.InsertItemAsync("emuType", "SideKick");
            store.InsertItemAsync("bridges", bData);
            store.InsertItemAsync("devices", Array.Empty<BaseDevice>());
            return store;
        }

        /// <summary>
        ///     Loads our data store from a dynamic path, and tries to get the item
        /// </summary>
        /// <param name="key"></param>
        /// <returns>dynamic object corresponding to key, or null if not found</returns>
        public static dynamic GetItem(string key) {
            try {
                using var dStore = new DataStore(GetConfigPath("store.json"));
                var output = dStore.GetItem(key);
                return output;
            }
            catch (KeyNotFoundException) { }

            return null;
        }
        

        public static dynamic GetItem<T>(string key) {
            try {
                using var dStore = new DataStore(GetConfigPath("store.json"));
                dynamic output = dStore.GetItem<T>(key);
                return output;
            }
            catch (KeyNotFoundException) { }

            return null;
        }

        public static void SetItem(string key, dynamic value) {
            using var dStore = new DataStore(GetConfigPath("store.json"));
            dStore.ReplaceItem(key, value, true);
        }

        public static void SetItem<T>(string key, dynamic value) {
            using var dStore = new DataStore(GetConfigPath("store.json"));
            dStore.ReplaceItem<T>(key, value, true);
        }

        public static string GetStoreSerialized() {
            var jsonPath = GetConfigPath("store.json");
            if (!File.Exists(jsonPath)) return null;
            try {
                return File.ReadAllText(jsonPath);
            }
            catch (IOException e) {
                Console.WriteLine($@"An IO Exception occurred: {e.Message}.");
            }

            return null;
        }

        public static BaseDevice GetDeviceData() {
            using var dd = GetStore();
            BaseDevice dev;
            string devType = dd.GetItem("emuType");
            if (devType == "SideKick")
                dev = dd.GetItem<SideKick>("myDevice");
            else
                dev = dd.GetItem<Connect>("myDevice");
            return dev;
        }

        /// <summary>
        ///     Determine if config path is local, or docker
        /// </summary>
        /// <param name="filePath">Config file to check</param>
        /// <returns>Modified path to config file</returns>
        private static string GetConfigPath(string filePath) {
            // If no etc dir, return normal path
            if (!Directory.Exists("/etc/huedream")) return filePath;
            // Make our etc path for docker
            var newPath = "/etc/huedream/" + filePath;
            // If the config file doesn't exist locally, we're done
            if (!File.Exists(filePath)) return newPath;
            // Otherwise, move the config to etc
            Console.WriteLine($@"Moving file from {filePath} to {newPath}");
            File.Copy(filePath, newPath);
            File.Delete(filePath);
            return newPath;
        }

        private static string GetLocalIpAddress() {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
            throw new Exception("No network adapters found in " + JsonConvert.SerializeObject(host));
        }

        private static void UpgradeBridgeStorage() {
            if (GetItem("bridges") != null) return;
            var bIp = GetItem("hueIp");
            var bUser = GetItem("hueUser");
            var bKey = GetItem("hueKey");
            List<Group> entGroups = GetItem<List<Group>>("entertainmentGroups");
            var entGroup = GetItem("entertainmentGroup");
            List<KeyValuePair<int, string>> lights = GetItem("hueLights");
            
            var bList = HueBridge.FindBridges();
            var bData = new List<BridgeData>();
            foreach (var lb in bList) {
                if (lb.IpAddress != bIp) continue;
                Console.WriteLine(@"Upgrading bridge storage for existing bridge.");
                var bd = new BridgeData(lb.IpAddress, lb.BridgeId, bUser, bKey);
                bd.SetGroups(entGroups.ToArray());
                bd.SetLights(lights.ToArray());
                bd.EntertainmentGroup = entGroup;
                bData.Add(bd);
            }

            bData.AddRange(from lb in bList where lb.IpAddress != bIp select new BridgeData(lb.IpAddress, lb.BridgeId));
            SetItem("bridges", bData.ToArray());
        }
    }
}