using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Accord.IO;
using HueDream.Models.DreamGrab;
using HueDream.Models.DreamScreen;
using HueDream.Models.DreamScreen.Devices;
using HueDream.Models.Hue;
using HueDream.Models.Nanoleaf;
using HueDream.Models.Util;
using JsonFlatFileDataStore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HueDream.Models {
    [Serializable]
    public static class DreamData {
        public static DataStore GetStore() {
            var path = GetConfigPath("store.json");
            var store = new DataStore(path);
            store = CheckDefaults(store);
            return store;
        }

        /// <summary>
        ///     Loads our data store from a dynamic path, and tries to get the item
        /// </summary>
        /// <param name="key"></param>
        /// <returns>dynamic object corresponding to key, or default if not found</returns>
        public static dynamic GetItem(string key) {
            try {
                var dStore = GetStore();
                var output = dStore.GetItem(key);
                dStore.Dispose();
                return output;
            }
            catch (KeyNotFoundException) { }
            return null;
        }
        
        
        public static dynamic GetItem<T>(string key) {
            try {
                using var dStore = GetStore();
                var output = dStore.GetItem<T>(key);
                dStore.Dispose();
                return output;
            } catch (KeyNotFoundException e) {
                Console.WriteLine($@"Value not found: {e.Message}");
                return null;
            }
        }

        
        private static DataStore CheckDefaults(DataStore store) {
                
            var v = store.GetItem("defaultsSet");
            if (v == null) SetDefaults(store);
            return store;
        }

        private static DataStore SetDefaults(DataStore store) {
            LogUtil.Write("Setting defaults.");
            store.InsertItem("dataSource", "DreamScreen");
            store.InsertItem("camWidth", 1920);
            store.InsertItem("camHeight", 1080);
            store.InsertItem("camMode", 1);
            store.InsertItem("scaleFactor", .5);
            store.InsertItem("showSource", false);
            store.InsertItem("showEdged", false);
            store.InsertItem("showWarped", false);
            store.InsertItem("emuType", "SideKick");
            store.InsertItem("captureMode", 0);
            store.InsertItem("camType", 1);
            BaseDevice myDevice = new SideKick(GetLocalIpAddress());
            myDevice.Initialize();
            store.InsertItem("myDevice", myDevice);
            var lData = new LedData(true);
            store.InsertItem("ledData", lData);
            store.InsertItem("minBrightness", 0);
            store.InsertItem("saturationBoost", 0);
            store.InsertItem("dsIp", "0.0.0.0");
            ScanDevices(store);
            store.InsertItem("defaultsSet", true);
            return store;
        }

        public static void SetItem(string key, dynamic value) {
            using var dStore = GetStore();
            dStore.ReplaceItem(key, value, true);
        }

        public static void SetItem<T>(string key, dynamic value) {
            using var dStore = GetStore();
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
            string devType = dd.GetItem("devType");
            if (devType == "SideKick") {
                dev = dd.GetItem<SideKick>("myDevice");
            } else if (devType == "DreamVision") {
                dev = dd.GetItem<DreamScreen4K>("myDevice");
            } else {
                dev = dd.GetItem<Connect>("myDevice");
            }
            
            return dev;
        }


        public static (int, int) GetTargetLeds() {
            var dsIp = GetItem<string>("dsIp");
            var devices = GetItem<List<BaseDevice>>("devices");
            foreach (var dev in devices) {
                var tsIp = dev.IpAddress;
                LogUtil.Write("TSIP: " + tsIp);
                if (tsIp == dsIp) {
                    LogUtil.Write("We have a matching IP");
                    var fs = dev.flexSetup;
                    var dX = fs[0];
                    var dY = fs[1];
                    LogUtil.Write($@"DX, DY: {dX} {dY}");
                    return (dX, dY);
                }
            }
            return (0,0);
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
        
        public static void RefreshDevices() {
            // Get dream devices
            var dreamDevices = DreamDiscovery.FindDevices();
            var leaves = NanoDiscovery.Refresh();
            // Find bridges
            var bridges = HueBridge.GetBridgeData();
                SetItem("devices", dreamDevices);
                SetItem("bridges", bridges);
                SetItem("leaves", leaves);
            
        }
        
        public static void ScanDevices(DataStore store) {
            // Get dream devices
            var dreamDevices = DreamDiscovery.FindDevices();
            var leaves = NanoDiscovery.Discover();
            // Find bridges
            var bridges = HueBridge.FindBridges();
            store.InsertItem("devices", dreamDevices);
            store.InsertItem("bridges", bridges);
            store.InsertItem("leaves", leaves);
        }

        public static void RefreshPublicIp() {
            var myIp = Dns.GetHostEntry(Dns.GetHostName()).AddressList[0].ToString();  
            LogUtil.Write("My IP Address is :" + myIp);
        }
    }
}