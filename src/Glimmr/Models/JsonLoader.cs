#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Glimmr.Models.ColorSource.Ambient;
using Glimmr.Models.ColorSource.Audio;
using Glimmr.Models.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

#endregion

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

			if (Directory.Exists(userPath)) {
				_directories.Add(userPath);
			}
		}

		public bool ImportJson(string path) {
			if (!File.Exists(path)) return false;
			Log.Debug($"Loading scene from path: {path}");
			using StreamReader r = new(path);
			string json = r.ReadToEnd();
			var ids = new List<int>();
			
			try {
				var scene = JsonConvert.DeserializeObject<AudioScene>(json);
				var scenes = LoadFiles<AudioScene>();
				ids.AddRange(scenes.Select(sc => sc.Id));
				if (ids.Count > 0) {
					scene.Id = ids.Max() + 1;
					return SaveJson(scene, scene.Name, "audioScenes");
				}
			} catch (Exception) {
				Log.Debug("Can't import JSON as audio scene.");
			}
			try {
				var scene = JsonConvert.DeserializeObject<AmbientScene>(json);
				if (scene == null) {
					Log.Warning("Unable to import scene as ambient.");
					return false;
				}
				var scenes = LoadFiles<AmbientScene>();
				ids.AddRange(scenes.Select(sc => sc.Id));
				if (ids.Count > 0) {
					scene.Id = ids.Max() + 1;
					return SaveJson(scene, scene.Name, "ambientScenes");
				}
			} catch (Exception) {
				Log.Debug("Can't import JSON as audio scene.");
			}
			
			return false;
		}

		private static bool SaveJson(dynamic scene, string name, string path) {
			if (scene == null) {
				return false;
			}
			var userPath = Path.Join(SystemUtil.GetUserDir(), path);
			if (!Directory.Exists(userPath)) {
				Directory.CreateDirectory(userPath);
			}

			if (Directory.Exists(path)) {
				var filePath = Path.Join(userPath, $"{name}.json");
				Log.Debug("Saving scene to " + filePath);
				try {
					File.WriteAllText(filePath, JsonConvert.SerializeObject(scene));
					return true;
				} catch (Exception) {
					Log.Warning("Exception saving file to " + filePath);
					return false;
				}
			}

			return false;
		}

		public List<T> LoadFiles<T>() {
			var output = new List<T>();
			var dirIndex = 0;
			var fCount = 50;
			foreach (var dir in _directories.Where(Directory.Exists)) {
				foreach (var file in Directory.EnumerateFiles(dir)) {
					if (file.Contains(".json")) {
						try {
							var data = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(file));
							if (data != null) {
								if (dirIndex != 0) {
									var id = data.GetValue("Id");
									var name = data.GetValue("Name");
									if (id != null && name != null) {
										if ((int) id == 0 && (string) name! != "Random") {
											data["Id"] = fCount;
										}
									} else {
										continue;
									}
								}

								var obj = data.ToObject<T>();
								if (obj != null) {
									output.Add(obj);
								}
							}
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

		public List<T> LoadDynamic<T>() {
			var files = LoadFiles<T>();
			return files.Where(f => f != null).ToList();
		}

		public AudioScene GetItem(dynamic id) {
			var files = LoadFiles<JObject>();
			foreach (var f in files) {
				if (!f.TryGetValue("Id", out var check)) {
					continue;
				}

				if (check == id) {
					return f.ToObject<AudioScene>();
				}
			}

			return GetDefault();
		}

		private dynamic GetDefault() {
			var files = LoadFiles<AudioScene>();
			return files.Count != 0 ? files[0] : new AudioScene();
		}
	}
}