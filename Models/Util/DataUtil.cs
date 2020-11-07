using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using HueDream.Models.DreamScreen;
using HueDream.Models.DreamScreen.Devices;
using HueDream.Models.LED;
using HueDream.Models.StreamingDevice.Hue;
using HueDream.Models.StreamingDevice.LIFX;
using HueDream.Models.StreamingDevice.Nanoleaf;
using HueDream.Models.StreamingDevice.WLed;
using JsonFlatFileDataStore;
using LifxNet;
using ManagedBass;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HueDream.Models.Util {
    [Serializable]
    public static class DataUtil {
        public static bool scanning { get; set; }
        public static DataStore GetStore() {
            var path = GetConfigPath("store.json");
            // Check that our store is actually valid
            DataStore store;
            try {
                store = new DataStore(path);
                try {
                    string lastBackup = store.GetItem("lastBackup");
                    if (string.IsNullOrEmpty(lastBackup)) {
                        CreateBackup(path);
                    } else {
                        var lDate = DateTime.Parse(lastBackup, CultureInfo.InvariantCulture);
                        if (lDate - DateTime.Now > TimeSpan.FromMinutes(30)) {
                            CreateBackup(path);
                        }
                    }
                } catch (Exception e) {
                    LogUtil.Write("An exception occurred fetching last backup date: " + e.Message);
                }
                return store;
            } catch (Exception e) {
                LogUtil.Write("Store Read Exception: " + e.Message, "WARN");
            }
        
            // If we couldn't read our store, restore from backup and try again
            var restored = RestoreBackup(path);
            if (restored) {
                try {
                    store = new DataStore(path);
                    return store;
                } catch {
                    LogUtil.Write("Well, this is really bad. We couldn't restore our restored store from storage.", "ERROR");
                }
            }
            // Nuclear option, we should never actually get here.
            File.Delete(path);
            var tStore = new DataStore(path);
            var lifxClient = LifxClient.CreateAsync().Result;
            CheckDefaults(tStore, lifxClient);
            lifxClient.Dispose();
            return tStore;
        }

        private static bool RestoreBackup(string storePath) {
            bool restored;
            var location = Path.GetDirectoryName(storePath);
            if (string.IsNullOrEmpty(location)) {
                location = ".";
            }
            var sep = Path.DirectorySeparatorChar;
            var backupDir = location + sep + "backup";
            var backupFile = backupDir + sep + "store.json.bak";
            if (File.Exists(backupFile)) {
                try {
                    var tStore = new DataStore(backupFile);
                    File.Copy(backupFile, storePath, true);
                    tStore.Dispose();
                    restored = true;
                    LogUtil.Write("Backup restored.");
                } catch (Exception e) {
                    LogUtil.Write("An exception occurred restoring backup datastore: " + e.Message, "ERROR");
                    restored = false;
                }
            } else {
                LogUtil.Write("NO BACKUP STORE FOUND, CREATING NEW STORE!!", "ERROR");
                LogUtil.Write("NO BACKUP STORE FOUND, CREATING NEW STORE!!", "ERROR");
                LogUtil.Write("NO BACKUP STORE FOUND, CREATING NEW STORE!!", "ERROR");
                File.Delete(storePath);
                var tStore = new DataStore(storePath);
                var lifxClient = LifxClient.CreateAsync().Result;
                CheckDefaults(tStore, lifxClient);
                tStore.Dispose();
                lifxClient.Dispose();
                LogUtil.Write("NEW STORE CREATED.", "WARN");
                restored = true;
            }

            return restored;
        }

        /// <summary>
        /// Create a backup of the existing datastore.
        /// DO NOT CALL THIS unless you've already validated the JSON is good.
        /// Due to the use case, we don't want to call it from the method
        /// because then we will be creating unnecessary I/O.
        /// </summary>
        /// <param name="storePath">The location of the validated datastore to back up.</param>
        /// <returns>True if backup was successful</returns>
        private static bool CreateBackup(string storePath) {
            bool done;
            // Create our paths
            var location = Path.GetDirectoryName(storePath);
            if (string.IsNullOrEmpty(location)) {
                location = ".";
            }
            var sep = Path.DirectorySeparatorChar;
            var backupDir = location + sep + "backup";
            var backupFile = backupDir + sep + "store.json.bak";
            // We don't need to check dir exists, this method just does it
            LogUtil.Write("Creating backup to " + backupFile);
            Directory.CreateDirectory(backupDir);
            try {
                var dstore = new DataStore(storePath);
                dstore.InsertItem("lastBackup", DateTime.Now.ToString(CultureInfo.InvariantCulture));
                dstore.Dispose();
                File.Copy(storePath, backupFile, true);
                done = true;
            } catch (Exception e) {
                LogUtil.Write("An exception was thrown during file backup: " + e.Message);
                done = false;
            }
            return done;
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

        public static string GetDeviceSerial() {
            var serial = string.Empty;
            try {
                serial = GetItem("serial");
            } catch (KeyNotFoundException) {
                
            }

            if (string.IsNullOrEmpty(serial)) {
                Random rd = new Random();
                serial = "12091" + rd.Next(0, 9) + rd.Next(0, 9) + rd.Next(0, 9);
                SetItem("serial", serial);
            }

            return serial;
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
        

        public static DataStore CheckDefaults(DataStore store, LifxClient lc) {
            var v = store.GetItem("defaultsSet");
            if (v == null) store = SetDefaults(store, lc).Result;
            return store;
        }

        private static async Task<DataStore> SetDefaults(DataStore store, LifxClient lc) {
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
            await ScanDevices(store, lc).ConfigureAwait(false);
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


        public static async void RefreshDevices(LifxClient c) {
            var cs = new CancellationTokenSource();
            cs.CancelAfter(10000);
            LogUtil.Write("Starting scan.");
            scanning = true;
            // Get dream devices
            var ld = new LifxDiscovery(c);
            var nanoTask = NanoDiscovery.Refresh(cs.Token);
            var bridgeTask = HueDiscovery.Refresh(cs.Token);
            var dreamTask = DreamDiscovery.Discover();
            var wLedTask = WledDiscovery.Discover();
            var bulbTask = ld.Refresh(cs.Token);
            try {
                await Task.WhenAll(nanoTask, bridgeTask, dreamTask, bulbTask, wLedTask);
            } catch (TaskCanceledException e) {
                LogUtil.Write("Discovery task was canceled before completion: " + e.Message, "WARN");
            } catch (SocketException f) {
                LogUtil.Write("Socket Exception during discovery: " + f.Message, "WARN");
            }
				
            LogUtil.Write("Refresh complete.");
            try {
                SetItem<List<NanoData>>("leaves", nanoTask.Result);
                SetItem<List<BridgeData>>("bridges", bridgeTask.Result);
                //SetItem<List<WLedData>>("wled", wLedTask.Result);
                // We don't need to store dream devices because of janky discovery. Maybe fix this...
                SetItem<List<LifxData>>("lifxBulbs", bulbTask.Result);
            } catch (TaskCanceledException) {

            } catch (AggregateException) {
                
            }

            scanning = false;
            cs.Dispose();
        }

        public static async Task ScanDevices(DataStore store, LifxClient lc) {
            if (store == null) throw new ArgumentException("Invalid store.");
            if (scanning) return;
            scanning = true;
            // Get dream devices
            var ld = new LifxDiscovery(lc);
            var nanoTask = NanoDiscovery.Discover();
            var hueTask = HueDiscovery.Discover();
            var dreamTask = DreamDiscovery.Discover();
            var wLedTask = WledDiscovery.Discover();
            var bulbTask = ld.Discover(5);
            await Task.WhenAll(nanoTask, hueTask, dreamTask, bulbTask).ConfigureAwait(false);
            var leaves = await nanoTask.ConfigureAwait(false);
            var bridges = await hueTask.ConfigureAwait(false);
            var dreamDevices = await dreamTask.ConfigureAwait(false);
            var bulbs = await bulbTask.ConfigureAwait(false);
            var wleds = await wLedTask.ConfigureAwait(false);
            await store.InsertItemAsync("bridges", bridges).ConfigureAwait(false);
            await store.InsertItemAsync("leaves", leaves).ConfigureAwait(false);
            await store.InsertItemAsync("devices", dreamDevices).ConfigureAwait(false);
            await store.InsertItemAsync("lifxBulbs", bulbs).ConfigureAwait(false);
            await store.InsertItemAsync("wled", wleds).ConfigureAwait(false);
            store.Dispose();
            scanning = false;
        }

        public static void RefreshPublicIp() {
            var myIp = Dns.GetHostEntry(Dns.GetHostName()).AddressList[0].ToString();
            LogUtil.Write("My IP Address is :" + myIp);
        }
    }
}