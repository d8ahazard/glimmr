using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Glimmr.Models.ColorSource.Ambient;
using Glimmr.Models.ColorTarget;
using Glimmr.Models.ColorTarget.DreamScreen;
using Glimmr.Models.ColorTarget.Hue;
using Glimmr.Models.ColorTarget.LED;
using Glimmr.Models.ColorTarget.LIFX;
using Glimmr.Models.ColorTarget.Nanoleaf;
using Glimmr.Models.ColorTarget.Wled;
using Glimmr.Services;
using LiteDB;
using Microsoft.AspNetCore.Authentication;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using Color = System.Drawing.Color;

namespace Glimmr.Models.Util {
    [Serializable]
    public static class DataUtil {
        public static bool Scanning { get; set; }
        private static LiteDatabase _db;
        
        public static LiteDatabase GetDb() {
            if (_db == null) _db = new LiteDatabase(@"./store.db");
            return _db;
        }

        public static void Dispose() {
            Log.Debug("Disposing database...");
            _db?.Commit();
            _db?.Dispose();
            Log.Debug("Database disposed.");
        }

        public static async Task CheckDefaults(ControlService cs) {
            var db = GetDb();
            var sObj = GetObject<SystemData>("SystemData");
            if (sObj == null) {
                var sd = new SystemData {DefaultSet = true};
                SetObject<SystemData>("SystemData",sd);
                Log.Warning("Setting default values!");
                // If not, create it
                var deviceIp = IpUtil.GetLocalIpAddress();
                var myDevice = new DreamData {Id = deviceIp, IpAddress = deviceIp};
                Log.Debug("Creating default device data: " + JsonConvert.SerializeObject(myDevice));
                SetDeviceData(myDevice);
                SetObject<Color>("AmbientColor", Color.FromArgb(255, 255, 255, 255));
                Log.Debug("Creating DreamData profile...");
                // Get/create our collection of Dream devices
                var d = db.GetCollection<DreamData>("Dev_Dreamscreen");
                // Create our default device
                // Save it
                d.Upsert(myDevice.Id, myDevice);
                d.EnsureIndex(x => x.Id);
                db.Commit();
            } else {
                Log.Information("Default values are already set, continuing.");
            }

            var ledData = GetCollection<LedData>("LedData");
            if (ledData == null || ledData.Count != 3) {
                Log.Debug("Creating LED Data");
                var l1 = new LedData();
                var l2 = new LedData();
                var l3 = new LedData();
                l1.Count = 0;
                l2.Count = 0;
                l3.Count = 0;
                l1.GpioNumber = 18;
                l2.GpioNumber = 19;
                l3.GpioNumber = 10;
                l1.Id = "0";
                l2.Id = "1";
                l3.Id = "2";
                
                await InsertCollection<LedData>("LedData", l1);
                await InsertCollection<LedData>("LedData", l2);
                await InsertCollection<LedData>("LedData", l3);
            }
        }
       
        //fixed
        public static List<dynamic> GetCollection(string key) {
            try {
                var db = GetDb();
                var coll = db.GetCollection(key);
                var output = new List<dynamic>();
                if (coll == null) return output;
                output.AddRange(coll.FindAll());
                return output;
            } catch (Exception e) {
                Log.Warning($@"Get exception for {key}:", e);
                return null;
            }
        }
        //fixed
        public static List<T> GetCollection<T>() where T : class {
            try {
                var db = GetDb();
                var coll = db.GetCollection<T>();
                var output = new List<T>();
                if (coll == null) return output;
                output.AddRange(coll.FindAll());
                return output;
            } catch (Exception e) {
                Log.Debug($@"Get exception for {typeof(T)}: {e.Message}");
                return null;
            }
        }
        //fixed
        public static List<T> GetCollection<T>(string key) where T : class {
            var output = new List<T>();
            try {
                var db = GetDb();
                var coll = db.GetCollection<T>(key);
                if (coll == null) return output;
                output.AddRange(coll.FindAll());
            } catch (Exception e) {
                Log.Warning("Exception: " + e.Message);
            }

            return output;
            
        }
        //fixed
        public static dynamic GetCollectionItem<T>(string key, string value) where T : new() {
            try {
                var db = GetDb();
                var coll = db.GetCollection<T>(key);
                    var r = coll.FindById(value);
                    return r;
                
            } catch (Exception e) {
                Log.Debug($@"Get exception for {typeof(T)}: {e.Message}");
                return null;
            }
        }
        //fixed
        public static async Task InsertCollection<T>(string key, dynamic value) where T: class {
            var db = GetDb();
            var coll = db.GetCollection<T>(key);
            await Task.FromResult(coll.Upsert(value.Id, value));
            db.Commit();
        }
        //fixed
        public static async Task InsertCollection(string key, dynamic value) {
                var db = GetDb();
                var coll = db.GetCollection(key);
                await Task.FromResult(coll.Upsert(value.Id, value));
                db.Commit();
        }

        public static List<IColorTargetData> GetDevices() {
            return GetObject<List<IColorTargetData>>("Devices");
        }
        
        public static List<T> GetDevices<T>(string tag) where T : class {
            var devs = GetObject<List<IColorTargetData>>("Devices");
            var output = new List<T>();
            foreach (var d in devs) {
                if (d.Tag == tag) {
                    output.Add((T)d);
                }
            }
            return output;
        }

        public static dynamic GetDevice<T>(string id) where T : class {
            var devs = GetObject<List<IColorTargetData>>("Devices");
            foreach (var d in devs) {
                if (d.Id == id) {
                    return (T)d;
                }
            }
            return null;
        }


        public static dynamic GetDevice(string id) {
            var devs = GetObject<List<IColorTargetData>>("Devices");
            foreach (var d in devs) {
                if (d.Id == id) {
                    return d;
                }
            }
            return null;
        }

        public static void AddDevice(dynamic device) {
            var devices = GetDevices();
            var merged = false;
            for (var i = 0; i < devices.Count; i++) {
                if (devices[i].Id != device.Id) {
                    continue;
                }

                devices[i] = device;
                merged = true;
            }
            if (!merged) devices.Add(device);
            SetObject<List<IColorTargetData>>("Devices", devices);
        }
        
        public static string GetDeviceSerial() {
            var serial = string.Empty;
            try {
                serial = GetItem("Serial");
            } catch (KeyNotFoundException) {

            }

            if (string.IsNullOrEmpty(serial)) {
                Random rd = new Random();
                serial = "12091" + rd.Next(0, 9) + rd.Next(0, 9) + rd.Next(0, 9);
                SetItem("Serial", serial);
            }

            return serial;
        }


        public static dynamic GetDeviceById(string id) {
            var db = GetDb();
            try {
                var coll0 = db.GetCollection<NanoleafData>("LedData");
                var bDev0 = coll0.FindById(id);
                if (bDev0 != null) return bDev0;
                
                var coll = db.GetCollection<HueData>("Dev_Hue");
                var bDev = coll.FindById(id);
                if (bDev != null) return bDev;

                var coll1 = db.GetCollection<LifxData>("Dev_Lifx");
                var bDev1 = coll1.FindById(id);
                if (bDev1 != null) return bDev1;

                var coll2 = db.GetCollection<NanoleafData>("Dev_Nanoleaf");
                var bDev2 = coll2.FindById(id);
                if (bDev2 != null) return bDev2;

                var coll3 = db.GetCollection<WledData>("Dev_Wled");
                var bDev3 = coll3.FindById(id);
                if (bDev3 != null) return bDev3;

                var coll4 = db.GetCollection<NanoleafData>("Dev_Dreamscreen");
                var bDev4 = coll4.FindById(id);
                if (bDev4 != null) return bDev4;
            } catch (Exception e) {
                Log.Debug("Exception getting device data: " + e.Message);
            }

            return null;

        }

        
        public static string GetStoreSerialized() {
            var db = GetDb();
            var cols = db.GetCollectionNames();
            var output = new Dictionary<string, List<dynamic>>();
            foreach (var col in cols) {
                var collection = db.GetCollection(col);
                var list = collection.FindAll().ToList();
                var lList = new List<dynamic>();
                foreach (var l in list) {
                    var jObj = LiteDB.JsonSerializer.Serialize(l);
                    var json = JObject.Parse(jObj);
                    lList.Add(json);
                }
                output[col] = lList;
            }
            var jl = new JsonLoader("ambientScenes");
            output["AmbientScenes"] = jl.LoadDynamic<AmbientScene>();
            return JsonConvert.SerializeObject(output);
        }

        public static DreamData GetDeviceData() {
            try {
                var myDevice = GetObject<DreamData>("MyDevice");
                if (myDevice != null) return myDevice;
            } catch (Exception e) {
                Log.Debug("Caught: " + e.Message);
            }

            var devIp = IpUtil.GetLocalIpAddress();
            var newDevice = new DreamData {Id = devIp, IpAddress = devIp};
            var db = GetDb();
            var col = db.GetCollection<DreamData>("MyDevice");
            col.Upsert(newDevice.Id, newDevice);
            db.Commit();
            SetDeviceData(newDevice);
            return newDevice;
        }

        public static void SetDeviceData(DreamData myDevice) {
            if (string.IsNullOrEmpty(myDevice.Id)) {
                myDevice.Id = IpUtil.GetLocalIpAddress();
                myDevice.IpAddress = IpUtil.GetLocalIpAddress();
            }
            var values = new[]{"AmbientMode","DeviceMode","AmbientShow","DeviceGroup","GroupName"};
            var existing = GetDeviceData();
            foreach (var v in myDevice.GetType().GetProperties()) {
                if (!values.Contains(v.Name)) continue;
                foreach (var e in existing.GetType().GetProperties()) {
                    if (e.Name != v.Name) continue;
                    if (e.GetValue(existing) != v.GetValue(myDevice)) {
                        SetItem(v.Name, v.GetValue(myDevice));
                    }
                }
            }

            var db = GetDb();
            var col = db.GetCollection<DreamData>("MyDevice");
            col.Upsert(myDevice.Id, myDevice);
            db.Commit();
        }

        public static void SetItem(string key, dynamic value) {
            var db = GetDb();
            // See if it's a system property
            var sd = GetObject<SystemData>("SystemData");
            var saveSd = false;
            var saveDd = false;
            foreach (var e in sd.GetType().GetProperties()) {
                if (e.Name != key) continue;
                saveSd = true;
                e.SetValue(sd, value);
            }

            if (saveSd) {
                SetObject<SystemData>("SystemData", sd);
            } else {
                var dd = GetDeviceData();
                foreach (var e in dd.GetType().GetProperties()) {
                    if (e.Name != key) continue;
                    saveDd = true;
                    e.SetValue(dd, value);
                }
                if (saveDd) SetDeviceData(dd);
            }

            if (saveSd || saveDd) db.Commit();
        }
       
        public static dynamic GetItem<T>(string key) {
            var i = GetItem(key);
            if (i == null) {
                return null;
            }
            return (T) GetItem(key);
        }
        
        public static dynamic GetItem(string key) {
            var sd = GetObject<SystemData>("SystemData");
            foreach (var e in sd.GetType().GetProperties()) {
                if (e.Name != key) continue;
                return e.GetValue(sd);
            }

            var dd = GetDeviceData();
            foreach (var e in dd.GetType().GetProperties()) {
                if (e.Name != key) continue;
                return e.GetValue(dd);
            }

            return null;
        }
        
        public static dynamic GetObject<T>(string key) {
            try {
                var db = GetDb();
                var col = db.GetCollection<T>(key);
                if (col.Count() == 0) return null;
                foreach (var doc in col.FindAll()) {
                    return doc;
                }
            } catch (Exception e) {
                Log.Warning("Exception: " + e.Message);
            }

            return null;
        }
        
        public static void SetObject<T>(string key, dynamic value) {
            var db = GetDb();
            var col = db.GetCollection<T>(key);
            col.Upsert(0, value);
            db.Commit();
        }


        public static List<DreamData> GetDreamDevices() {
            var dd = GetDb();
            var devs = dd.GetCollection<DreamData>("Dev_Dreamscreen");
            var dl = devs.FindAll();
            return dl.ToList();
        }

        public static DreamData GetDreamDevice(string id) {
            return GetDreamDevices().FirstOrDefault(dev => dev.Id == id);
        }

        public static (int, int) GetTargetLights() {
            var db = GetDb();
            var dsIp = GetItem("DsIp");
            var devices = db.GetCollection<DreamData>("Dev_Dreamscreen").FindAll();
            foreach (var dev in devices) {
                var tsIp = dev.IpAddress;
                Log.Debug("Device IP: " + tsIp);
                if (tsIp != dsIp) continue;
                Log.Debug("We have a matching IP");
                var fs = dev.FlexSetup;
                var dX = fs[0];
                var dY = fs[1];
                Log.Debug($@"DX, DY: {dX} {dY}");
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
            Log.Debug($@"Moving file from {filePath} to {newPath}");
            File.Copy(filePath, newPath);
            File.Delete(filePath);
            return newPath;
        }

        public static void RefreshPublicIp() {
            var myIp = Dns.GetHostEntry(Dns.GetHostName()).AddressList[0].ToString();
            Log.Debug("My IP Address is :" + myIp);
        }
    }
}