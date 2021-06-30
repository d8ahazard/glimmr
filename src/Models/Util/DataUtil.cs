#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Glimmr.Models.ColorSource.Ambient;
using Glimmr.Models.ColorSource.Audio;
using Glimmr.Models.ColorTarget;
using LiteDB;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using JsonSerializer = LiteDB.JsonSerializer;

#endregion

namespace Glimmr.Models.Util {
	[Serializable]
	public static class DataUtil {
		private static bool _dbLocked;
		private static List<dynamic>? _devices;
		private static LiteDatabase _db = GetDb();
		private static SystemData _systemData = CacheSystemData();

		public static LiteDatabase GetDb() {
			while (_dbLocked) {
				Log.Debug("Awaiting export...");
				Task.Delay(TimeSpan.FromMilliseconds(50));
			}

			var userPath = SystemUtil.GetUserDir();
			userPath = Path.Join(userPath, "store.db");

			if (File.Exists("./store.db")) {
				Log.Information($"Migrating existing datastore to {userPath}.");
				if (!File.Exists(userPath)) {
					File.Copy("./store.db", userPath, false);
				}

				File.Delete("./store.db");
			}

			return _db ??= new LiteDatabase(userPath);
		}

		public static void Dispose() {
			_db.Commit();
			_db.Dispose();
		}


		//fixed
		public static List<dynamic>? GetCollection(string key) {
			try {
				var db = GetDb();
				var coll = db.GetCollection(key);
				var output = new List<dynamic>();
				if (coll == null) {
					return output;
				}

				output.AddRange(coll.FindAll());
				return output;
			} catch (Exception e) {
				Log.Warning($@"Get exception for {key}:", e.Message);
				return null;
			}
		}

		//fixed
		public static List<T>? GetCollection<T>() where T : class {
			try {
				var db = GetDb();
				var coll = db.GetCollection<T>();
				var output = new List<T>();
				if (coll == null) {
					return output;
				}

				output.AddRange(coll.FindAll());
				return output;
			} catch (Exception e) {
				Log.Warning($@"Get exception for {typeof(T)}: {e.Message}");
				return null;
			}
		}

		//fixed
		public static List<T> GetCollection<T>(string key) where T : class {
			var output = new List<T>();
			try {
				var db = GetDb();
				var coll = db.GetCollection<T>(key);
				if (coll == null) {
					return output;
				}

				output.AddRange(coll.FindAll());
			} catch (Exception e) {
				Log.Warning("Exception: " + e.Message);
			}

			return output;
		}

		//fixed
		public static dynamic? GetCollectionItem<T>(string key, string value) where T : new() {
			try {
				var db = GetDb();
				var coll = db.GetCollection<T>(key);
				var r = coll.FindById(value);
				return r;
			} catch (Exception e) {
				Log.Warning($@"Get exception for {typeof(T)}: {e.Message}");
				return null;
			}
		}

		//fixed
		public static async Task InsertCollection<T>(string key, dynamic value) where T : class {
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

		private static List<dynamic> CacheDevices() {
			var db = GetDb();
			var devs = new BsonDocument[0];
			var devices = new dynamic[0];
			try {
				devs = db.GetCollection("Devices").FindAll().ToArray();
				devices = db.GetCollection<dynamic>("Devices").FindAll().ToArray();
			} catch (Exception) {
				// Ignore
			}

			var output = new List<dynamic>(devices.Length);
			foreach (var device in devices) {
				foreach (var dev in devs) {
					try {
						var json = JsonSerializer.Serialize(dev);
						var jObj = JObject.Parse(json);
						if (jObj.GetValue("_id") != device.Id) {
							continue;
						}

						var type = jObj.GetValue("_type");
						if (type == null) {
							continue;
						}

						var typeType = Type.GetType(type.ToString());
						if (typeType == null) {
							continue;
						}

						dynamic? donor = Activator.CreateInstance(typeType);
						if (donor == null) {
							continue;
						}

						device.KeyProperties = donor.KeyProperties;
						output.Add(device);
					} catch (Exception e) {
						Log.Warning("Exception: " + e.Message);
					}
				}
			}

			_devices = output;
			return output;
		}

		public static List<dynamic> GetDevices() {
			return _devices ?? CacheDevices();
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
					output.Add((T) d);
				}
			}

			return output;
		}

		public static dynamic? GetDevice<T>(string id) where T : class {
			var devs = GetDevices();
			return (from d in devs where d.Id == id select (T) d).FirstOrDefault();
		}


		public static dynamic? GetDevice(string id) {
			var devs = GetDevices();
			return devs.FirstOrDefault(d => d.Id == id);
		}


		public static async Task AddDeviceAsync(dynamic device, bool merge = true) {
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
			CacheDevices();
			await Task.FromResult(true);
		}

		public static string GetDeviceSerial() {
			var serial = string.Empty;
			try {
				serial = GetItem("Serial");
			} catch (KeyNotFoundException) {
			}

			if (string.IsNullOrEmpty(serial)) {
				var rd = new Random();
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


		public static string ExportSettings() {
			var dbPath = "./store.db";
			var userDir = SystemUtil.GetUserDir();
			var stamp = DateTime.Now.ToString("yyyyMMddHHmm");
			var outFile = Path.Combine(userDir, $"store_{stamp}_.db");
			var output = string.Empty;
			_dbLocked = true;
			_db.Commit();
			_db.Dispose();
			try {
				File.Copy(dbPath, outFile);
				output = outFile;
			} catch (Exception) {
				//ignored
			}

			_db = new LiteDatabase(dbPath);
			_dbLocked = false;
			return output;
		}

		public static bool ImportSettings(string newPath) {
			var dbPath = "./store.db";
			var userDir = SystemUtil.GetUserDir();
			var stamp = DateTime.Now.ToString("yyyyMMddHHmm");
			var outFile = Path.Combine(userDir, $"store_{stamp}_.db");
			// lock DB so we don't get issues
			_dbLocked = true;
			_db.Commit();
			_db.Dispose();
			try {
				File.Copy(dbPath, outFile);
			} catch (Exception d) {
				Log.Warning("Exception backing up DB: " + d.Message);
			}

			if (File.Exists(outFile) && File.Exists(newPath)) {
				Log.Debug($"DB backed up to {outFile}, importing new DB.");
				try {
					File.Copy(newPath, dbPath, true);
					_db = new LiteDatabase(dbPath);
					_dbLocked = false;
					return true;
				} catch (Exception e) {
					Log.Warning("Exception copying file: " + e.Message);
				}
			}

			_db = new LiteDatabase(dbPath);
			_dbLocked = false;
			return false;
		}


		public static string GetStoreSerialized() {
			var output = new Dictionary<string, dynamic>();
			var sd = GetSystemData();
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
			var sd = _systemData;
			var saveSd = false;
			foreach (var e in sd.GetType().GetProperties()) {
				if (e.Name != key) {
					continue;
				}

				saveSd = true;
				e.SetValue(sd, value);
			}

			if (saveSd) {
				SetSystemData(sd);
			}

			if (saveSd) {
				db.Commit();
			}
		}

		public static dynamic? GetItem<T>(string key) {
			var i = GetItem(key);
			if (i == null) {
				return null;
			}
			return (T) i;
		}

		public static dynamic? GetItem(string key) {
			var sd = GetSystemData();
			foreach (var e in sd.GetType().GetProperties()) {
				if (e.Name != key) {
					continue;
				}

				return e.GetValue(sd);
			}

			return null;
		}

		public static dynamic? GetObject<T>(string key) {
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

			return null;
		}

		public static SystemData GetSystemData() {
			if (_systemData == null) {
				return CacheSystemData();
			}

			return _systemData;
		}

		private static SystemData CacheSystemData() {
			var db = GetDb();

			var col = db.GetCollection<SystemData>("SystemData");
			try {
				if (col.Count() != 0) {
					var cols = col.FindAll().ToList();
					foreach (var sda in cols) {
						_systemData = sda;
						return _systemData;
					}
				}
			} catch (Exception e) {
				Log.Warning("Exception: " + e.Message + " at " + e.StackTrace);
			}

			Log.Debug("Creating new SD");
			var sd = new SystemData();
			sd.SetDefaults();
			col.Upsert(0, sd);
			_systemData = sd;
			return sd;
		}

		public static void SetSystemData(SystemData value) {
			var db = GetDb();
			var col = db.GetCollection<SystemData>("SystemData");
			if (value.HSectors == 0) {
				value.HSectors = 5;
			}

			if (value.VSectors == 0) {
				value.HSectors = 3;
			}

			if (value.LeftCount == 0) {
				value.LeftCount = 24;
			}

			if (value.RightCount == 0) {
				value.LeftCount = 24;
			}

			if (value.TopCount == 0) {
				value.TopCount = 40;
			}

			if (value.BottomCount == 0) {
				value.BottomCount = 40;
			}

			col.Upsert(0, value);
			db.Commit();
			_systemData = value;
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
			col.Upsert("0", value);
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
			if (!Directory.Exists("/etc/glimmr")) {
				return filePath;
			}

			// Make our etc path for docker
			var newPath = "/etc/glimmr/" + filePath;
			// If the config file doesn't exist locally, we're done
			if (!File.Exists(filePath)) {
				return newPath;
			}

			// Otherwise, move the config to etc
			File.Copy(filePath, newPath);
			File.Delete(filePath);
			return newPath;
		}
	}
}