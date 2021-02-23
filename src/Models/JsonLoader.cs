using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Glimmr.Models.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace Glimmr.Models {
	public class JsonLoader {
		private readonly List<string> _directories;

		public JsonLoader(string path) {
			var appDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
			_directories = new List<string>();
			var appPath = Path.Join(appDir, path);
			_directories.Add(appPath);
			var userPath = Path.Join(SystemUtil.GetUserDir(), path);
			if (!Directory.Exists(userPath)) {
				try {
					Directory.CreateDirectory(userPath);
				} catch (Exception e) {
					Log.Debug("Directory creation exception: " + e.Message);
				}
			}
			if (Directory.Exists(userPath)) _directories.Add(userPath);
			Log.Debug("Directories: " + JsonConvert.SerializeObject(_directories));
		}

		public List<T> LoadFiles<T>() {
			var output = new List<T>();
			var dirIndex = 0;
			var fCount = 50;
			foreach (var dir in _directories.Where(Directory.Exists)) {
				foreach(var file in Directory.EnumerateFiles(dir)) {
					if (file.Contains(".json")) {
						try {
							var data = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(file));
							if (dirIndex != 0) {
								if ((int) data.GetValue("Id") == 0 && (string) data.GetValue("Name") != "Random") {
									data["Id"] = fCount;
								}
							}

							output.Add(data.ToObject<T>());
						} catch (Exception e) {
							Log.Warning($"Parse exception for {file}: " + e.Message);
						}
					}
					fCount++;
				}
				dirIndex++;
			}

			return output;
		}

		public List<dynamic> LoadDynamic<T>() {
			var output = new List<dynamic>();
			var files = LoadFiles<T>();
			foreach (var f in files) {
				output.Add((dynamic) f);
			}

			return output;
		}

		public dynamic GetItem<T>(dynamic id, bool getDefault = false) {
			
			var files = LoadFiles<JObject>();
			foreach (var f in files) {
				if (!f.TryGetValue("Id", out var check)) {
					continue;
				}

				if (check == id) {
					return f.ToObject<T>();
				}
			}

			return getDefault ? GetDefault<T>() : null;
		}

		private dynamic GetDefault<T>() {
			var files = LoadFiles<T>();
			if (files.Count != 0) {
				return files[0];
			}
			return null;
		}
	}
}