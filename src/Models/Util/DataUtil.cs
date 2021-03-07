using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
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
            if (_db == null) _db = new LiteDatabase(@"./store.db");
            return _db;
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
            DreamData dev = GetObject<DreamData>("MyDevice");
            var audio = GetCollection<AudioData>("Dev_Audio");
            var devices = GetDevices();
            var mons = DisplayUtil.GetMonitors();
            var exMons = GetCollection<MonitorInfo>("Dev_Video");
            var oMons = new List<MonitorInfo>();
            Log.Debug("Ex Mons: " + JsonConvert.SerializeObject(oMons));
            foreach (var mon in mons) {
                var exists = false;
                foreach (var cMon in exMons) {
                    if (mon.Id == cMon.Id) {
                        Log.Debug("Adding existing mon: " + JsonConvert.SerializeObject(cMon));
                        oMons.Add(cMon);
                        exists = true;
                    }
                }

                if (!exists) {
                    Log.Debug("Adding sys mon: " + JsonConvert.SerializeObject(mon));
                    oMons.Add(mon);
                }
            }
            var jl = new JsonLoader("ambientScenes");
            output["SystemData"] = sd;
            output["Devices"] = devices;
            output["MyDevice"] = dev;
            output["Dev_Audio"] = audio;
            output["Dev_Video"] = oMons;
            output["AmbientScenes"] = jl.LoadDynamic<AmbientScene>();
            output["AudioScenes"] = jl.LoadDynamic<AudioScene>();
            //Log.Debug("Returning serialized store: " + JsonConvert.SerializeObject(output));
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
            SetObject<SystemData>("SystemData",sd);
            Log.Warning("Setting default values!");
            // If not, create it
            var deviceIp = IpUtil.GetLocalIpAddress();
            var myDevice = new DreamData {Id = deviceIp, IpAddress = deviceIp};
            Log.Debug("Creating default device data: " + JsonConvert.SerializeObject(myDevice));
            SetDeviceData(myDevice);
            SetObject<Color>("AmbientColor", Color.FromArgb(255, 255, 255, 255));
            Log.Debug("Migrating devices");
            MigrateDevices().ConfigureAwait(true);
            Log.Debug("Done.");
            return sd;
        }
        
        public static void SetObject<T>(string key, dynamic value) {
            var db = GetDb();
            var col = db.GetCollection<T>(key);
            col.Upsert(0, value);
            db.Commit();
        }
        
        public static async Task SetObjectAsync<T>(string key, dynamic value) {
            var db = GetDb();
            Log.Debug("Getting col");
            var col = db.GetCollection<T>(key);
            Log.Debug("Upserting: " + JsonConvert.SerializeObject(value));
            col.Upsert(0, value);
            Log.Debug("Committing.");
            await Task.FromResult(true);
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

        public static object GetDevices<T>() {
            var devs = GetDevices();
            return devs.Where(dev => dev.GetType() == typeof(T)).Cast<T>().ToList();
        }
    }
}