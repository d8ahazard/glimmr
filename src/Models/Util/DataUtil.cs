using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Glimmr.Models.ColorSource.Ambient;
using Glimmr.Models.ColorSource.Audio;
using Glimmr.Models.ColorTarget;
using Glimmr.Models.ColorTarget.DreamScreen;
using Glimmr.Models.ColorTarget.Hue;
using Glimmr.Models.ColorTarget.Lifx;
using Glimmr.Models.ColorTarget.Wled;
using Glimmr.Models.ColorTarget.Yeelight;
using LiteDB;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using Color = System.Drawing.Color;

namespace Glimmr.Models.Util {
    [Serializable]
    public static class DataUtil {
        private static LiteDatabase _db;
        
        public static LiteDatabase GetDb() {
            return _db ??= new LiteDatabase(@"./store.db");
        }

        public static void Dispose() {
            Log.Debug("Disposing database...");
            _db?.Commit();
            _db?.Dispose();
            Log.Debug("Database disposed.");
        }

        
        private static async Task MigrateDevices() {
            var db = GetDb();
            var lifx = db.GetCollection<LifxData>("Dev_Lifx");
            var nano = db.GetCollection<LifxData>("Dev_Nanoleaf");
            var ds = db.GetCollection<LifxData>("Dev_Dreamscreen");
            var yee = db.GetCollection<YeelightData>("Dev_Yeelight");
            var hue = db.GetCollection<HueData>("Dev_Hue");
            var wled = db.GetCollection<WledData>("Dev_Wled");
            
            var devs = new dynamic[] {lifx, nano, ds, yee, hue, wled};
            
            foreach (var col in devs) {
                if (col == null) {
                    continue;
                }

                foreach (var dev in col.FindAll.toArray()) {
                    await AddDeviceAsync(dev);
                }
                db.DropCollection(col.Name);
            }

            db.Commit();

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

        public static List<dynamic> GetDevices() {
            var db = GetDb();
            var devs =  db.GetCollection("Devices").FindAll().ToArray();
            var devices = db.GetCollection<dynamic>("Devices").FindAll().ToArray();
            var output = new List<dynamic>();
            foreach (var device in devices) {
                foreach (var dev in devs) {
                    var json = LiteDB.JsonSerializer.Serialize(dev);
                    var jObj = JObject.Parse(json);
                    if (jObj.GetValue("_id") == device.Id) {
                        dynamic donor = Activator.CreateInstance(Type.GetType(jObj.GetValue("_type").ToString()));
                        device.KeyProperties = donor?.KeyProperties;
                        output.Add(device);
                    }
                }
            }
            return output;
        }

        public static void RemoveDevice(string id) {
            var db = GetDb();
            var devs = db.GetCollection("Devices");
            devs.Delete(id);
        }
        
        public static List<T> GetDevices<T>(string tag) where T : class {
            var devs = GetDevices();
            var output = new List<T>();
            foreach (var d in devs) {
                if (d.Tag == tag) {
                    output.Add((T)d);
                }
            }
            return output;
        }

        public static dynamic GetDevice<T>(string id) where T : class {
            var devs = GetDevices();
            return (from d in devs where d.Id == id select (T) d).FirstOrDefault();
        }


        public static dynamic GetDevice(string id) {
            var devs = GetDevices();
            return devs.FirstOrDefault(d => d.Id == id);
        }

        
        
        public static async Task AddDeviceAsync(dynamic device, bool merge=true) {
            var db = GetDb();
            var devs = db.GetCollection<dynamic>("Devices");
            if (merge) {
                var devices = devs.FindAll().ToArray();
                foreach (var t in devices) {
                    if (t.Id != device.Id.ToString()) {
                        continue;
                    }

                    IColorTargetData dev = t;
                    dev.UpdateFromDiscovered(device);
                    device = dev;
                }
            }

            device.LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
            devs.Upsert(device);
            db.Commit();
            await Task.FromResult(true);
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

        public static void DeleteDevice(string deviceId) {
            var db = GetDb();
            try {
                var devs = db.GetCollection<dynamic>("Devices");
                devs.Delete(deviceId);
                db.Commit();
                Log.Debug($"Device {deviceId} deleted.");
            } catch (Exception) {
                //ignored
            }
        }

      

        
        public static string GetStoreSerialized() {
            var output = new Dictionary<string, dynamic>();
            SystemData sd = GetObject<SystemData>("SystemData");
            DreamScreenData dev = GetObject<DreamScreenData>("MyDevice");
            var audio = GetCollection<AudioData>("Dev_Audio");
            var devices = GetDevices();
            var mons = DisplayUtil.GetMonitors();
            var exMons = GetCollection<MonitorInfo>("Dev_Video");
            var oMons = new List<MonitorInfo>();
            var caps = SystemUtil.ListUsb();
            foreach (var mon in mons) {
                var exists = false;
                foreach (var cMon in exMons) {
                    if (mon.Id == cMon.Id) {
                        oMons.Add(cMon);
                        exists = true;
                    }
                }

                if (!exists) {
                    oMons.Add(mon);
                }
            }
            var jl = new JsonLoader("ambientScenes");
            output["SystemData"] = sd;
            output["Devices"] = devices;
            output["MyDevice"] = dev;
            output["Dev_Audio"] = audio;
            output["Dev_Video"] = oMons;
            output["Dev_Usb"] = caps;
            output["AmbientScenes"] = jl.LoadDynamic<AmbientScene>();
            output["AudioScenes"] = jl.LoadDynamic<AudioScene>();
            return JsonConvert.SerializeObject(output);
        }

     
        public static void SetItem(string key, dynamic value) {
            var db = GetDb();
            // See if it's a system property
            var sd = GetObject<SystemData>("SystemData");
            var saveSd = false;
            foreach (var e in sd.GetType().GetProperties()) {
                if (e.Name != key) continue;
                saveSd = true;
                e.SetValue(sd, value);
            }

            if (saveSd) {
                SetObject<SystemData>(sd);
            }

            if (saveSd) db.Commit();
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
            return null;
        }
        
        public static dynamic GetObject<T>(string key) {
            try {
                var db = GetDb();
                var col = db.GetCollection<T>(key);
                if (col.Count() != 0) {
                    foreach (var doc in col.FindAll()) {
                        return doc;
                    }
                }
            } catch (Exception e) {
                Log.Warning("Exception: " + e.Message);
            }

            if (key == "SystemData") {
                Log.Debug("Creating new system data...");
                var sd = CreateSystemData();
                return sd;
            }
            return null;
        }

        private static SystemData CreateSystemData() {
            var sd = new SystemData {DefaultSet = true, CaptureRegion = DisplayUtil.GetDisplaySize()};
            Log.Debug("Object setting...");
            SetObject<SystemData>("SystemData");
            Log.Debug("Done.");
            return sd;
        }
        
        public static void SetObject<T>(dynamic value) {
            var db = GetDb();
            var key = typeof(T).Name;
            var col = db.GetCollection<T>(key);
            col.Upsert(0, value);
            db.Commit();
        }
        
        public static async Task SetObjectAsync<T>(dynamic value) {
            var db = GetDb();
            var key = typeof(T).Name;
            var col = db.GetCollection<T>(key);
            col.Upsert(0, value);
            await Task.FromResult(true);
            db.Commit();
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
            File.Copy(filePath, newPath);
            File.Delete(filePath);
            return newPath;
        }

        
        
    }
}