using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using HueDream.Models.DreamScreen;
using HueDream.Models.DreamScreen.Devices;
using HueDream.Models.Hue;
using HueDream.Models.LED;
using HueDream.Models.LIFX;
using HueDream.Models.Nanoleaf;
using JsonFlatFileDataStore;
using ManagedBass;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Q42.HueApi;
using Q42.HueApi.Models.Bridge;

namespace HueDream.Models.Util {
    [Serializable]
    public static class DataUtil {
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
        /// <param name="def"></param>
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
            } catch (Exception e) {
                LogUtil.Write($@"Value not found: {e.Message}");
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
            store.InsertItem("devType", "SideKick");
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
            BaseDevice myDevice = new SideKick(IpUtil.GetLocalIpAddress());
            myDevice.SetDefaults();
            store.InsertItem("myDevice", myDevice);
            var lData = new LedData(true);
            store.InsertItem("ledData", lData);
            store.InsertItem("minBrightness", 0);
            store.InsertItem("saturationBoost", 0);
            store.InsertItem("dsIp", "0.0.0.0");
            store.InsertItem("defaultsSet", true);
            store.InsertItem("audioDevices", new List<DeviceInfo>());
            store.InsertItem("audioThreshold", .01f);
            ScanDevices(store);
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
            } else if (devType == "DreamVision") {
                dev = dd.GetItem<DreamScreen4K>("myDevice");
            } else {
                dev = dd.GetItem<Connect>("myDevice");
            }

            if (string.IsNullOrEmpty(dev.AmbientColor)) {
                dev.AmbientColor = "FFFFFF";
            }
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
                        LogUtil.Write("Dev tag: " + tag);
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
            return output;
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
            if (!Directory.Exists("/etc/huedream")) return filePath;
            // Make our etc path for docker
            var newPath = "/etc/huedream/" + filePath;
            // If the config file doesn't exist locally, we're done
            if (!File.Exists(filePath)) return newPath;
            // Otherwise, move the config to etc
            LogUtil.Write($@"Moving file from {filePath} to {newPath}");
            File.Copy(filePath, newPath);
            File.Delete(filePath);
            return newPath;
        }


        public static async void RefreshDevices() {
            // Get dream devices
            var ld = new LifxDiscovery();
            var nanoTask = NanoDiscovery.Refresh();
            var bridgeTask = HueBridge.GetBridgeData();
            var dreamTask = DreamDiscovery.FindDevices();
            var bulbTask = ld.Refresh();
            LogUtil.Write("Tasks created...");
            await Task.WhenAll(nanoTask, bridgeTask, dreamTask, bulbTask).ConfigureAwait(false);
            LogUtil.Write("Await done?");
            var leaves = nanoTask.Result;
            var bridges = await bridgeTask.ConfigureAwait(false);
            var dreamDevices = await dreamTask.ConfigureAwait(false);
            var lifxDevices = await bulbTask.ConfigureAwait(false);
            ld.Dispose();
            LogUtil.Write("Vars acquired...");
            SetItem<List<BridgeData>>("bridges", bridges);
            SetItem<List<NanoData>>("leaves", leaves);
            SetItem<List<LifxData>>("lifxBulbs", lifxDevices);
            SetItem("devices", dreamDevices);
        }

        public static async Task ScanDevices(DataStore store) {
            // Get dream devices
            var ld = new LifxDiscovery();
            var nanoTask = NanoDiscovery.Discover();
            var hueTask = HueBridge.FindBridges();
            var dreamTask = DreamDiscovery.FindDevices();
            var bulbTask = ld.Discover(5);
            await Task.WhenAll(nanoTask, hueTask, dreamTask, bulbTask).ConfigureAwait(false);
            var leaves = await nanoTask.ConfigureAwait(false);
            var bridges = await hueTask.ConfigureAwait(false);
            var dreamDevices = await dreamTask.ConfigureAwait(false);
            var bulbs = await bulbTask.ConfigureAwait(false);
            ld.Dispose();
            await store.InsertItemAsync("bridges", bridges).ConfigureAwait(false);
            await store.InsertItemAsync("leaves", leaves).ConfigureAwait(false);
            await store.InsertItemAsync("devices", dreamDevices).ConfigureAwait(false);
            await store.InsertItemAsync("lifxBulbs", bulbs).ConfigureAwait(false);
            
        }

        public static void RefreshPublicIp() {
            var myIp = Dns.GetHostEntry(Dns.GetHostName()).AddressList[0].ToString();
            LogUtil.Write("My IP Address is :" + myIp);
        }
    }
}