#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
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
		private static LiteDatabase? _db = GetDb();
		private static SystemData? _systemData = CacheSystemData();

		private static LiteDatabase GetDb() {
			while (_dbLocked) {
				Log.Debug("Awaiting export...");
				Task.Delay(TimeSpan.FromMilliseconds(50));
			}

			var userPath = SystemUtil.GetUserDir();
			userPath = Path.Join(userPath, "store.db");

			if (_db != null) return _db;
			if (_db == null) {
				try {
					var db = new LiteDatabase(userPath);
					return db;
				} catch (Exception e) {
					Log.Warning("Exception creating database: " + e.Message);
				}
			}

			if (File.Exists(userPath)) {
				RollbackDb();
			}
			
			try {
				var db = new LiteDatabase(userPath);
				return db;
			} catch (Exception e) {
				Log.Warning("Exception creating database(2): " + e.Message);
				throw new InvalidOperationException("Can't create new db...gotta die.");
			}
		}

		public static void RollbackDb() {
			Log.Debug("Rolling back database.");
			// Get list of db files
			var userPath = SystemUtil.GetUserDir();
			var dbFiles = Directory.GetFiles(userPath, "*.db");
			var dbPath = Path.Join(userPath, "store.db");
			Array.Sort(dbFiles);
			Array.Reverse(dbFiles);
			var path = string.Empty;
			foreach (var p in dbFiles) {
				if (p.Contains("store.db") || p.Contains("store-log.db")) continue;
				try {
					var db = new LiteDatabase(p);
					if (!db.CollectionExists("SystemData")) {
						continue;
					}

					Log.Debug($"DB {p} appears valid?");
					path = p;
					break;
				} catch (Exception e) {
					Log.Warning($"Exception opening {p}: " + e.Message);
				}	
			}

			
			File.Move(dbPath, dbPath + ".bak");
			if (path == string.Empty) {
				return;
			}

			Log.Debug("Rotating db to " + path);
			File.Move(path, dbPath);

		}

		public static void Dispose() {
			_db?.Commit();
			_db?.Dispose();
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
				Log.Warning($"Exception finding collection {key}: " + e.Message);
			}

			return output;
		}

		//fixed
		public static dynamic? GetCollectionItem<T>(string key, string value) where T : new() {
			try {
				var db = GetDb();
				var coll = db.GetCollection<T>(key);
				if (coll != null) {
					var r = coll.FindById(value);
					return r;
				}
			} catch (Exception e) {
				Log.Warning($@"Get exception for {typeof(T)}: {e.Message}");
				return null;
			}

			return null;
		}

		//fixed
		public static async Task InsertCollection<T>(string key, dynamic value) where T : class {
			try {
				var db = GetDb();
				var coll = db.GetCollection<T>(key);
				if (coll != null) {
					await Task.FromResult(coll.Upsert(value.Id, value));
					db.Commit();
				}
			} catch (Exception e) {
				Log.Warning("Exception caught inserting: " + e.Message + " at " + e.StackTrace);
			}

		}

		//fixed
		public static async Task InsertCollection(string key, dynamic value) {
			try {
				var db = GetDb();
				var coll = db.GetCollection(key);
				if (coll != null) {
					await Task.FromResult(coll.Upsert(value.Id, value));
					db.Commit();	
				}
				
			} catch (Exception e) {
				Log.Warning("Exception caught inserting: " + e.Message + " at " + e.StackTrace);
			}
		}

		private static List<dynamic> CacheDevices() {
			var db = GetDb();
			var devs = Array.Empty<BsonDocument>();
			var devices = Array.Empty<dynamic>();
			try {
				devs = db.GetCollection("Devices").FindAll().ToArray();
				devices = db.GetCollection<dynamic>("Devices").FindAll().ToArray();
			} catch (Exception e) {
				Log.Warning("Exception caught caching devices: " + e.Message + " at " + e.StackTrace);
			}

			if (devices == null || devs == null) return new List<dynamic>();
			var output = new List<dynamic>(devices.Length);
			try {
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
							Log.Warning("Exception Caching Devices: " + e.Message + " at " + e.StackTrace);
						}
					}
				}
			} catch (Exception e) {
				Log.Warning("Exception caching devs: " + e.Message + " at " + e.StackTrace);
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
			devs?.Delete(id);
		}

		public static List<T> GetDevices<T>(string tag) where T : class {
			var devs = GetDevices();
			return (from d in devs where d.Tag == tag select (T) d).ToList();
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
			try {
				var db = GetDb();
				var devs = db.GetCollection<dynamic>("Devices");
				if (devs == null) {	
					Log.Warning("No devices...");
					return;
				}
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
				devs.EnsureIndex("Id");
				db.Commit();
				CacheDevices();
			} catch (Exception e) {
				Log.Warning("Exception adding device: " + e.Message + " at " + e.StackTrace);
			}

			await Task.FromResult(true);
		}

		public static string GetDeviceSerial() {
			var serial = string.Empty;
			try {
				serial = GetItem("Serial");
			} catch (KeyNotFoundException) {
			}

			if (!string.IsNullOrEmpty(serial)) {
				return serial;
			}

			var rd = new Random();
			serial = "12091" + rd.Next(0, 9) + rd.Next(0, 9) + rd.Next(0, 9);
			SetItem("Serial", serial);

			return serial;
		}

		public static void DeleteDevice(string deviceId) {
			var db = GetDb();
			try {
				var devs = db.GetCollection<dynamic>("Devices");
				if (devs == null) {
					Log.Warning("Null devices, can't delete!");
					return;
				}

				Log.Information(devs.Delete(deviceId) ? $"Device {deviceId} deleted." : "Unable to delete device?");

				db.Commit();
			} catch (Exception e) {
				Log.Warning("Error deleting device: " + e.Message);
			}
		}


		public static string BackupDb() {
			if (_db == null) {
				Log.Warning("DB is null, this is bad!!");
				return "";
			}
			const string? dbPath = "./store.db";
			var userDir = SystemUtil.GetUserDir();
			var stamp = DateTime.Now.ToString("yyyyMMdd");
			var outFile = Path.Combine(userDir, $"store_{stamp}.db");
			Log.Debug($"Locking DB, backing up to {outFile}");
			var output = string.Empty;
			_dbLocked = true;
			_db.Commit();
			_db.Dispose();
			try {
				Log.Debug("Copying...");
				File.Copy(dbPath, outFile);
				output = outFile;
				Log.Debug("Done.");
			} catch (Exception) {
				//ignored
			}
			Log.Debug("Creating new.");
			_db = new LiteDatabase(dbPath);
			Log.Debug("Unlocked...");
			_dbLocked = false;
			return output;
		}

		public static bool ImportSettings(string newPath) {
			var userDir = SystemUtil.GetUserDir();
			var stamp = DateTime.Now.ToString("yyyyMMdd");
			var userPath = SystemUtil.GetUserDir();
			var dbPath = Path.Join(userPath, "store.db");
			var outFile = Path.Combine(userDir, $"store_{stamp}.db");
			// lock DB so we don't get issues
			_dbLocked = true;
			_db?.Commit();
			_db?.Dispose();
			try {
				// Back up existing db
				if (File.Exists(outFile)) {
					var rand = new Random();
					File.Move(outFile, outFile + rand.Next());
				}
				File.Copy(dbPath, outFile);
			} catch (Exception d) {
				Log.Warning("Exception backing up DB: " + d.Message);
			}

			if (File.Exists(outFile) && File.Exists(newPath)) {
				Log.Debug($"DB backed up to {outFile}, importing new DB.");
				try {
					File.Copy(newPath, dbPath, true);
					GetDb();
					CacheDevices();
					CacheSystemData();
					_db = new LiteDatabase(dbPath);
					_dbLocked = false;
					return true;
				} catch (Exception e) {
					Log.Warning("Exception copying file: " + e.Message);
				}
			}

			_db = new LiteDatabase(dbPath);
			CacheDevices();
			CacheSystemData();
			_dbLocked = false;
			return false;
		}


		public static string GetStoreSerialized() {
			var output = new Dictionary<string, dynamic>();
			var sd = GetSystemData();
			var audio = GetCollection<AudioData>("Dev_Audio");
			var devices = GetDevices();

			var caps = SystemUtil.ListUsb();

			var jl1 = new JsonLoader("ambientScenes");
			var jl2 = new JsonLoader("audioScenes");
			output["SystemData"] = sd;
			output["Devices"] = devices;
			output["Dev_Audio"] = audio;
			output["Dev_Usb"] = caps;
			output["AmbientScenes"] = jl1.LoadDynamic<AmbientScene>();
			output["AudioScenes"] = jl2.LoadDynamic<AudioScene>();
			var assembly = Assembly.GetEntryAssembly();
			if (assembly != null) {
				var attrib = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
				if (attrib != null) {
					output["Version"] = attrib.InformationalVersion;
				}
			} else {
				output["Version"] = "0.0.0.0";
			}

			return JsonConvert.SerializeObject(output);
		}


		public static void SetItem(string key, dynamic value) {
			var db = GetDb();
			// See if it's a system property
			var sd = _systemData;
			var saveSd = false;
			if (sd == null) {
				Log.Warning("NO SD, this is bad!");
				return;
			}
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
			return (from e in sd.GetType().GetProperties() where e.Name == key select e.GetValue(sd)).FirstOrDefault();
		}

		public static dynamic? GetObject<T>(string key) {
			try {
				var db = GetDb();
				if (db == null) {
					Log.Warning("Can't get db, can't set object...");
					return null;
				}
				var col = db.GetCollection<T>(key);
				
				if (col.Count() != 0) {
					foreach (var doc in col.FindAll()) {
						return doc;
					}
				}
			} catch (Exception e) {
				Log.Warning("Exception Getting Data Object: " + e.Message + " at " + e.StackTrace);
			}

			return null;
		}

		public static SystemData GetSystemData() {
			return _systemData ?? CacheSystemData();
		}

		private static SystemData CacheSystemData() {
			var db = GetDb();
			if (db == null) {
				Log.Warning("Can't get db, can't set object...");
				throw new Exception("Can't get database, time to die.");
			}
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
				Log.Warning("Exception caching SD: " + e.Message + " at " + e.StackTrace);
			}
			
			Log.Information("Creating first-time SystemData.");
			var sd = new SystemData();
			sd.SetDefaults();
			
			try {
				col.Upsert(0, sd);
			} catch (Exception e) {
				Log.Warning("Exception updating SD: " + e.Message);
			}
			
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

			if (col != null) {
				col.Upsert(0, value);
				db.Commit();	
			} else {
				Log.Warning("Unable to insert, col is null. This is bad!");
			}
			
			_systemData = value;
		}


		public static void SetObject<T>(dynamic value) {
			var db = GetDb();
			if (db == null) {
				Log.Warning("Can't get db, can't set object...");
				return;
			}
			var key = typeof(T).Name;
			var col = db.GetCollection<T>(key);
			col.Upsert(0, value);
			db.Commit();
		}

		public static async Task SetObjectAsync<T>(dynamic value) {
			var db = GetDb();
			if (db == null) {
				Log.Warning("Can't get db, can't set object...");
				return;
			}
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