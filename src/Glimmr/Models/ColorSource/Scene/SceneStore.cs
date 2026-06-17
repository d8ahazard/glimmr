#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Glimmr.Models.ColorSource.Ambient;
using Glimmr.Models.Helper;
using Glimmr.Models.Util;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.Scene;

/// <summary>
///     Loads and persists <see cref="SceneV2" /> definitions. Bundled "system" scenes live in
///     the app's <c>scenes</c> folder; user scenes are written to the user data dir's
///     <c>scenes</c> folder. Scene ids are strings (GUIDs / slugs), so this deliberately does
///     not reuse <see cref="Helper.JsonLoader" /> (which assumes integer ids).
/// </summary>
public class SceneStore {
	private const string DirName = "scenes";
	private readonly string _appDir;
	private readonly string _userDir;

	public SceneStore() {
		var appBase = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? ".";
		_appDir = Path.Join(appBase, DirName);
		_userDir = Path.Join(SystemUtil.GetUserDir(), DirName);
		EnsureDir(_userDir);
	}

	private static void EnsureDir(string dir) {
		try {
			if (!Directory.Exists(dir)) {
				Directory.CreateDirectory(dir);
			}
		} catch (Exception e) {
			Log.Debug("Scene dir creation exception: " + e.Message);
		}
	}

	/// <summary>
	///     Load all scenes. User scenes shadow system scenes with the same id.
	/// </summary>
	public List<SceneV2> List() {
		var byId = new Dictionary<string, SceneV2>(StringComparer.OrdinalIgnoreCase);
		// App dir first (system scenes), then user dir so user copies win.
		foreach (var dir in new[] { _appDir, _userDir }) {
			if (!Directory.Exists(dir)) {
				continue;
			}

			foreach (var file in Directory.EnumerateFiles(dir, "*.json")) {
				try {
					var scene = JsonConvert.DeserializeObject<SceneV2>(File.ReadAllText(file));
					if (scene == null || string.IsNullOrEmpty(scene.Id)) {
						continue;
					}

					scene.System = ReferenceEquals(dir, _appDir);
					byId[scene.Id] = scene;
				} catch (Exception e) {
					Log.Warning($"Scene parse exception for {file}: " + e.Message);
				}
			}
		}

		// Built-in "Solid Color" scene (color comes from the system ambient color at play time)
		// and the legacy ambient image scenes (Image2Scene matrices), surfaced as image scenes so
		// everything lives under one unified "Scenes" mode.
		byId.TryAdd("solid", new SceneV2 {
			Id = "solid", Name = "Solid Color", Kind = "color", System = true, Width = 1, Height = 1
		});

		// Bundled bespoke-provider animations (Tetris, Pac-Man, Lightning, ...).
		foreach (var b in BuiltInScenes.All()) {
			byId.TryAdd(b.Id, b);
		}

		foreach (var amb in LoadAmbientImages()) {
			byId.TryAdd(amb.Id, amb);
		}

		return byId.Values.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
	}

	/// <summary>
	///     Map ambient "Matrix" scenes (2D images, e.g. from Image2Scene) into image
	///     <see cref="SceneV2" /> entries so they play through the unified Scene mode.
	/// </summary>
	private static IEnumerable<SceneV2> LoadAmbientImages() {
		var output = new List<SceneV2>();
		try {
			var loader = new JsonLoader("ambientScenes");
			foreach (var a in loader.LoadDynamic<AmbientScene>()) {
				if (a?.ColorMatrix == null || a.ColorMatrix.Length == 0 ||
				    !string.Equals(a.Mode, "Matrix", StringComparison.OrdinalIgnoreCase)) {
					continue;
				}

				output.Add(new SceneV2 {
					Id = "ambient-" + a.Id,
					Name = a.Name,
					Kind = "image",
					System = true,
					Width = a.ColorMatrix[0].Length,
					Height = a.ColorMatrix.Length,
					ColorMatrix = a.ColorMatrix,
					MatrixDirection = a.MatrixDirection ?? "Random",
					MatrixStep = a.MatrixStep,
					AnimationTime = a.AnimationTime
				});
			}
		} catch (Exception e) {
			Log.Warning("Failed to load ambient image scenes: " + e.Message);
		}

		return output;
	}

	public SceneV2? Get(string id) {
		return string.IsNullOrEmpty(id) ? null : List().FirstOrDefault(s => s.Id == id);
	}

	/// <summary>
	///     Resolve the absolute path of a scene's frame blob (may not exist yet).
	/// </summary>
	public string BlobPath(SceneV2 scene) {
		var file = string.IsNullOrEmpty(scene.FramesFile) ? scene.Id + ".bin.gz" : scene.FramesFile;
		// Prefer the user copy if present, otherwise fall back to the bundled location.
		var userPath = Path.Join(_userDir, file);
		if (File.Exists(userPath)) {
			return userPath;
		}

		var appPath = Path.Join(_appDir, file);
		return File.Exists(appPath) ? appPath : userPath;
	}

	/// <summary>
	///     Persist a (user) scene to disk. Returns the saved scene (with an assigned id).
	/// </summary>
	public SceneV2 Save(SceneV2 scene) {
		if (string.IsNullOrEmpty(scene.Id)) {
			scene.Id = Guid.NewGuid().ToString("N");
		}

		scene.System = false;
		EnsureDir(_userDir);
		var path = Path.Join(_userDir, scene.Id + ".json");
		File.WriteAllText(path, JsonConvert.SerializeObject(scene, Formatting.Indented));
		return scene;
	}

	/// <summary>
	///     The directory user scene blobs are written to.
	/// </summary>
	public string UserDir => _userDir;

	public bool Delete(string id) {
		var scene = Get(id);
		if (scene == null || scene.System) {
			return false;
		}

		var ok = false;
		var json = Path.Join(_userDir, id + ".json");
		try {
			if (File.Exists(json)) {
				File.Delete(json);
				ok = true;
			}

			if (!string.IsNullOrEmpty(scene.FramesFile)) {
				var blob = Path.Join(_userDir, scene.FramesFile);
				if (File.Exists(blob)) {
					File.Delete(blob);
				}
			}
		} catch (Exception e) {
			Log.Warning($"Exception deleting scene {id}: " + e.Message);
		}

		return ok;
	}
}
