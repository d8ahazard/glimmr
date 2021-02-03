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
		private List<string> _directories;

		public JsonLoader(string path) {
			var appDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
			_directories = new List<string>();
			if (appDir != String.Empty) _directories.Add(Path.Join(appDir, "..", path));
			_directories.Add(Path.Join(SystemUtil.GetUserDir(), path));
			Log.Debug("Directories: " + JsonConvert.SerializeObject(_directories));
		}

		public List<T> LoadFiles<T>() {
			var output = new List<JObject>();
			var realOutput = new List<T>();
			var dirIndex = 0;
			var fCount = 0;
			foreach (var dir in _directories.Where(Directory.Exists)) {
				foreach(var file in Directory.EnumerateFiles(dir)) {
					if (file.Contains(".json")) {
						try {
							var data = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(file));
							output.Add(data);
						} catch (Exception e) {
							Log.Warning($"Parse exception for {file}: " + e.Message);
						}
					}
				}
				try {

					// Loop through project files, which have an ID. Count, then add to non-proj files to increment ID
					foreach (var o in output) {
						if (dirIndex != 0) {
							if ((int) o.GetValue("Id") == 0 && (string) o.GetValue("Name") != "Random") {
								o["Id"] = fCount;
							}
						}
						realOutput.Add(o.ToObject<T>());
						fCount++;
					}
				} catch {
					//ignore
				}
				dirIndex++;
			}

			return realOutput;
		}

		public List<dynamic> LoadDynamic<T>() {
			var output = new List<dynamic>();
			var files = LoadFiles<T>();
			foreach (var f in files) {
				output.Add((dynamic) f);
			}

			return output;
		}
	}
}