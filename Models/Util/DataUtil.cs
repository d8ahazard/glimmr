using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Accord.IO;
using HueDream.Models.DreamScreen;
using HueDream.Models.DreamScreen.Devices;
using HueDream.Models.LED;
using HueDream.Models.StreamingDevice.Hue;
using HueDream.Models.StreamingDevice.LIFX;
using HueDream.Models.StreamingDevice.Nanoleaf;
using HueDream.Models.Util;
using JsonFlatFileDataStore;
using ManagedBass;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HueDream.Models.Util {
    [Serializable]
    public static class DataUtil {
        public static bool scanning { get; set; }
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
            } catch (KeyNotFoundException) {
                return null;
            }
        }

       
        public static dynamic GetItem<T>(string key) {
            try {
                using var dStore = GetStore();
                var output = dStore.GetItem<T>(key);
                dStore.Dispose();
                return output;
            } catch (KeyNotFoundException e) {
                LogUtil.Write($@"Get exception for {key}: {e.Message}");
                return null;
            }
        }
        
        public static List<dynamic> GetCollection(string key) {
            try {
                using var dStore = GetStore();
                var coll = dStore.GetCollection(key);
                var output = new List<dynamic>();
                if (coll == null) return output;
                output.AddRange(coll.AsQueryable());
                dStore.Dispose();
                return output;
            } catch (Exception e) {
                LogUtil.Write($@"Get exception for {key}: {e.Message}");
                return null;
            }
        }
        public static List<T> GetCollection<T>() where T : class {
            try {
                using var dStore = GetStore();
                var coll = dStore.GetCollection<T>();
                var output = new List<T>();
                if (coll == null) return output;
                output.AddRange(coll.AsQueryable());
                dStore.Dispose();
                return output;
            } catch (Exception e) {
                LogUtil.Write($@"Get exception for {typeof(T)}: {e.Message}");
                return null;
            }
        }

        
                
        public static List<T> GetCollection<T>(string key) where T : class {
            
                using var dStore = GetStore();
                var coll = dStore.GetCollection<T>(key);
                var output = new List<T>();
                if (coll == null) return output;
                output.AddRange(coll.AsQueryable());
                dStore.Dispose();
                return output;
            
        }
        
        public static dynamic GetCollectionItem<T>(string key, dynamic value) where T : class {
            try {
                using var dStore = GetStore();
                var coll = dStore.GetCollection<T>(key);
                IEnumerable<T> res =  coll.Find(value);
                dStore.Dispose();
                return res.FirstOrDefault();
            } catch (Exception e) {
                LogUtil.Write($@"Get exception for {typeof(T)}: {e.Message}");
                return null;
            }
        }


        
        public static void InsertCollection<T>(string key, dynamic value) where T: class {
            var dStore = GetStore();
            try {
                
                var coll = dStore.GetCollection<T>(key);
                if (coll == null) {
                    var list = new List<T>();
                    list.Add(value);
                    dStore.InsertItem(key, list);
                } else {
                    coll.ReplaceOne(value.Id, value, true);
                }

                dStore.Dispose();
            } catch (NullReferenceException e) {
                LogUtil.Write($@"Insert exception (typed) for {typeof(T)}: {e.Message} : {e.GetType()}");
                var list = dStore.GetItem<List<T>>(key) ?? new List<T>();
                list.Add(value);
                SetItem(key,list);
            }
            dStore.Dispose();
        }
        
        public static void InsertCollection(string key, dynamic value) {
            try {
                using var dStore = GetStore();
                var coll = dStore.GetCollection(key);
                coll.ReplaceOne(value.Id, value, true);
                dStore.Dispose();
            } catch (Exception e) {
                LogUtil.Write($@"Insert (notype) exception for {key}: {e.Message}");
            }
        }

        public static void InsertDsDevice(BaseDevice dev) {
            if (dev == null) throw new ArgumentException("Invalid device.");
            var ex = GetDreamDevices();
            var newList = ex.Select(c => c.Id == dev.Id ? dev : c).ToList();
            SetItem<List<BaseDevice>>("devices", newList);
        }
        

        private static DataStore CheckDefaults(DataStore store) {
            var v = store.GetItem("defaultsSet");
            if (v == null) store = SetDefaults(store).Result;
            return store;
        }

        private static async Task<DataStore> SetDefaults(DataStore store) {
            LogUtil.Write("Setting defaults.");
            BaseDevice myDevice = new SideKick(IpUtil.GetLocalIpAddress());
            myDevice.SetDefaults();
            var lData = new LedData(true);
            await store.InsertItemAsync("dataSource", "DreamScreen").ConfigureAwait(false);
            await store.InsertItemAsync("devType", "SideKick").ConfigureAwait(false);
            await store.InsertItemAsync("camWidth", 1920).ConfigureAwait(false);
            await store.InsertItemAsync("camHeight", 1080).ConfigureAwait(false);
            await store.InsertItemAsync("camMode", 1).ConfigureAwait(false);
            await store.InsertItemAsync("scaleFactor", .5).ConfigureAwait(false);
            await store.InsertItemAsync("showSource", false).ConfigureAwait(false);
            await store.InsertItemAsync("showEdged", false).ConfigureAwait(false);
            await store.InsertItemAsync("showWarped", false).ConfigureAwait(false);
            await store.InsertItemAsync("emuType", "SideKick").ConfigureAwait(false);
            await store.InsertItemAsync("captureMode", 0).ConfigureAwait(false);
            await store.InsertItemAsync("camType", 1).ConfigureAwait(false);
            await store.InsertItemAsync("myDevice", myDevice).ConfigureAwait(false);
            await store.InsertItemAsync("ledData", lData).ConfigureAwait(false);
            await store.InsertItemAsync("minBrightness", 0).ConfigureAwait(false);
            await store.InsertItemAsync("saturationBoost", 0).ConfigureAwait(false);
            await store.InsertItemAsync("dsIp", "0.0.0.0").ConfigureAwait(false);
            await store.InsertItemAsync("audioDevices", new List<DeviceInfo>()).ConfigureAwait(false);
            await store.InsertItemAsync("audioThreshold", .01f).ConfigureAwait(false);
            await store.InsertItemAsync("defaultsSet", true).ConfigureAwait(false);
            await ScanDevices(store).ConfigureAwait(false);
            LogUtil.Write("All data defaults have been set.");
            return store;
        }

        public static void SetItem(string key, dynamic value) {
            using var dStore = GetStore();
            dStore.ReplaceItem(key, value, true);
            dStore.Dispose();
        }

        public static void SetItem<T>(string key, dynamic value) {
            using var dStore = GetStore();
            dStore.ReplaceItem<T>(key, value, true);
            dStore.Dispose();
        }

        public static string GetStoreSerialized() {
            var jsonPath = GetConfigPath("store.json");
            if (!File.Exists(jsonPath)) return null;
            try {
                return File.ReadAllText(jsonPath);
            } catch (IOException e) {
                LogUtil.Write($@"An IO Exception occurred: {e.Message}.");
            }

            return null;
        }

        public static BaseDevice GetDeviceData() {
            using var dd = GetStore();
            BaseDevice dev;
            string devType = dd.GetItem("devType");
            LogUtil.Write("DeviceData fetched, we have a " + devType);
            if (devType == "SideKick") {
                dev = dd.GetItem<SideKick>("myDevice");
            } else if (devType == "DreamScreen4K") {
                dev = dd.GetItem<DreamScreen4K>("myDevice");
            } else {
                dev = dd.GetItem<Connect>("myDevice");
            }

            if (string.IsNullOrEmpty(dev.AmbientColor)) {
                dev.AmbientColor = "FFFFFF";
            }
            dd.Dispose();
            return dev;
        }

        public static List<BaseDevice> GetDreamDevices() {
            using var dd = GetStore();
            var output = new List<BaseDevice>();
            var dl = GetItem<List<JToken>>("devices");
            if (dl == null) return output;
            foreach (JObject dev in dl) {
                foreach (var pair in dev) {
                    var key = pair.Key;
                    if (key == "tag") {
                        var tag = pair.Value.ToString();
                        switch (tag) {
                            case "SideKick":
                                output.Add(dev.ToObject<SideKick>());
                                break;
                            case "Connect":
                                output.Add(dev.ToObject<Connect>());
                                break;
                            case "DreamScreen":
                                output.Add(dev.ToObject<DreamScreenHd>());
                                break;
                            case "DreamScreen4K":
                                output.Add(dev.ToObject<DreamScreen4K>());
                                break;
                            case "DreamScreenSolo":
                                output.Add(dev.ToObject<DreamScreenSolo>());
                                break;
                        }
                    }
                }
            }
            dd.Dispose();
            return output;
        }

        public static BaseDevice GetDreamDevice(string id) {
            return GetDreamDevices().FirstOrDefault(dev => dev.Id == id);
        }

        public static (int, int) GetTargetLights() {
            var dsIp = GetItem<string>("dsIp");
            var devices = GetItem<List<BaseDevice>>("devices");
            foreach (var dev in devices) {
                var tsIp = dev.IpAddress;
                LogUtil.Write("Device IP: " + tsIp);
                if (tsIp != dsIp) continue;
                LogUtil.Write("We have a matching IP");
                var fs = dev.flexSetup;
                var dX = fs[0];
                var dY = fs[1];
                LogUtil.Write($@"DX, DY: {dX} {dY}");
                return (dX, dY);
            }

            return (0, 0);
        }

        /// <summary>
        ///     Determine if config path is local, or docker
        /// </summary>
        /// <param name="filePath">Config file to check</param>
        /// <returns>Modified path to config file</returns>
        private static string GetConfigPath(string filePath) {
            // If no etc dir, return normal path
            if (!Directory.Exists("/etc/glimmr")) return filePath;
            // Make our etc path for docker
            var newPath = "/etc/glimmr/" + filePath;
            // If the config file doesn't exist locally, we're done
            if (!File.Exists(filePath)) return newPath;
            // Otherwise, move the config to etc
            LogUtil.Write($@"Moving file from {filePath} to {newPath}");
            File.Copy(filePath, newPath);
            File.Delete(filePath);
            return newPath;
        }


        public static async void RefreshDevices() {
            if (scanning) {
                LogUtil.Write("We are already scanning...hold your horses.", "WARN");
                return;
            } else {
                LogUtil.Write("Starting scan.");
            }
            scanning = true;
            // Get dream devices
            var ld = new LifxDiscovery();
            var nanoTask = NanoDiscovery.Refresh();
            var bridgeTask = HueDiscovery.Refresh();
            var dreamTask = DreamDiscovery.Discover();
            var bulbTask = ld.Refresh();
            await Task.WhenAll(nanoTask, bridgeTask, dreamTask, bulbTask).ConfigureAwait(false);
            LogUtil.Write("Refresh complete.");
            scanning = false;
        }

        public static async Task ScanDevices(DataStore store) {
            if (store == null) throw new ArgumentException("Invalid store.");
            if (scanning) return;
            scanning = true;
            // Get dream devices
            var ld = new LifxDiscovery();
            var nanoTask = NanoDiscovery.Discover();
            var hueTask = HueDiscovery.Discover();
            var dreamTask = DreamDiscovery.Discover();
            var bulbTask = ld.Discover(5);
            await Task.WhenAll(nanoTask, hueTask, dreamTask, bulbTask).ConfigureAwait(false);
            var leaves = await nanoTask.ConfigureAwait(false);
            var bridges = await hueTask.ConfigureAwait(false);
            var dreamDevices = await dreamTask.ConfigureAwait(false);
            var bulbs = await bulbTask.ConfigureAwait(false);
            await store.InsertItemAsync("bridges", bridges).ConfigureAwait(false);
            await store.InsertItemAsync("leaves", leaves).ConfigureAwait(false);
            await store.InsertItemAsync("devices", dreamDevices).ConfigureAwait(false);
            await store.InsertItemAsync("lifxBulbs", bulbs).ConfigureAwait(false);
            store.Dispose();
            scanning = false;
        }

        public static void RefreshPublicIp() {
            var myIp = Dns.GetHostEntry(Dns.GetHostName()).AddressList[0].ToString();
            LogUtil.Write("My IP Address is :" + myIp);
        }
    }
}