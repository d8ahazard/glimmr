using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using HueDream.Models.DreamGrab;
using HueDream.Models.DreamScreen.Devices;
using HueDream.Models.Hue;
using HueDream.Models.Util;
using JsonFlatFileDataStore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HueDream.Models {
    [Serializable]
    public static class DreamData {
        public static DataStore GetStore() {
            var path = GetConfigPath("store.json");
            var createDefaults = !File.Exists(path);
            var store = new DataStore(path);
            var output = createDefaults ? SetDefaults(store) : store;
            try {
                if (output.GetItem<LedData>("ledData") == null) {
                    output.InsertItem("ledData", new LedData());
                }
            } catch (KeyNotFoundException) {
                output.InsertItem("ledData", new LedData(true));
            }
            return output;
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

        private static DataStore SetDefaults(DataStore store) {
            store.InsertItem("dsIp", "0.0.0.0");
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
            store.InsertItem("devices", Array.Empty<BaseDevice>());

            BaseDevice myDevice = new SideKick(GetLocalIpAddress());
            myDevice.Initialize();
            var bList = HueBridge.FindBridges();
            var bData = bList.Select(lb => new BridgeData(lb.IpAddress, lb.BridgeId)).ToList();
            var lData = new LedData(true);
            store.InsertItem("myDevice", myDevice);
            store.InsertItem("bridges", bData);
            store.InsertItem("ledData", lData);
            return store;
        }


        public static dynamic GetItem<T>(string key) {
            try {
                using var dStore = new DataStore(GetConfigPath("store.json"));
                dynamic output = dStore.GetItem<T>(key);
                return output;
            }
            catch (NullReferenceException e) {
                Console.WriteLine($@"Value not found: {e.Message}");
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


        public static (int, int) GetTargetLeds() {
            var dsIp = GetItem<string>("dsIp");
            var devs = GetItem<List<BaseDevice>>("devices");
            foreach (var dev in devs) {
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
    }
}