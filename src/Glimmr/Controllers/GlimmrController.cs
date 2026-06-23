#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Models.Backup;
using Glimmr.Models.ColorSource.Monitorr;
using Glimmr.Models.ColorSource.Ambient;
using Glimmr.Models.ColorSource.Audio;
using Glimmr.Models.ColorSource.Audio.Engine;
using Glimmr.Models.ColorSource.Scene;
using Glimmr.Models.ColorSource.Scene.Script;
using Glimmr.Models.ColorTarget;
using Glimmr.Models.ColorTarget.Glimmr;
using Glimmr.Models.Data;
using Glimmr.Models.Helper;
using Glimmr.Models.Util;
using Glimmr.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Log = Serilog.Log;

#endregion

namespace Glimmr.Controllers;

[Route("api/[controller]")]
[ApiController]
public class GlimmrController : ControllerBase {
	private readonly ControlService _controlService;

	public GlimmrController() {
		var cs = ControlService.GetInstance();
		_controlService = cs;
	}

	/// <summary>
	///     Default endpoint - returns a SystemData object.
	/// </summary>
	/// <returns>
	///     <see cref="SystemData" />
	/// </returns>
	[HttpGet("")]
	public ActionResult<SystemData> GetSystemData() {
		Log.Debug("SD req");
		var sd = DataUtil.GetSystemData();
		return sd;
	}

	/// <summary>
	///     Retrieve the currently set ambient color.
	///     Will still return a value if ambient mode is not
	///     active.
	/// </summary>
	/// <returns>A HTML RGB hex string (#FFFFFF).</returns>
	[HttpGet("ambientColor")]
	public ActionResult<string> GetAmbientColor() {
		var sd = DataUtil.GetSystemData();
		return sd.AmbientColor;
	}


	/// <summary>
	///     Set the current ambient mode, and set device mode to ambient.
	/// </summary>
	/// <param name="color">A HTML-formatted RGB Color (#666666/FFFFFF)</param>
	/// <returns>The ID of the selected scene available, or -1 if not found.</returns>
	[HttpPost("ambientColor")]
	public async Task<ActionResult<string>> SetAmbientColor([FromBody] string color) {
		var sd = DataUtil.GetSystemData();
		if (color[..1] != "#" && color.Length == 6) {
			color = "#" + color;
		}

		if (!Regex.Match(color, "^#(?:[0-9a-fA-F]{3}){1,2}$").Success) {
			return Ok(sd.AmbientColor);
		}

		sd.AmbientColor = color.Replace("#", "");
		sd.AmbientScene = -1;
		sd.DeviceMode = DeviceMode.Ambient;
		await _controlService.UpdateSystem(sd);
		return Ok(sd.AmbientColor);
	}

	/// <summary>
	///     Retrieve the ambient scene specified in the query, or current if none is specified.
	///     Will still return a value if ambient mode is not
	///     active.
	/// </summary>
	/// <returns>The current ambient scene.</returns>
	[HttpGet("ambientScene")]
	public ActionResult<AmbientScene> GetAmbientScene([FromQuery] int? sceneId) {
		var sd = DataUtil.GetSystemData();
		sceneId ??= sd.AmbientScene;
		var storeD = new StoreData();
		foreach (var scene in storeD.AmbientScenes) {
			if (scene.Id == sceneId) {
				return new ActionResult<AmbientScene>(scene);
			}
		}

		return NotFound();
	}

	/// <summary>
	///     Retrieve the list of available ambient scenes.
	/// </summary>
	/// <returns>An array of ambient scenes.</returns>
	[HttpGet("ambientScenes")]
	public ActionResult<AmbientScene[]> GetAmbientScenes() {
		var sd = new StoreData();
		return sd.AmbientScenes;
	}

	/// <summary>
	///     Set the current ambient mode, and set device mode to ambient.
	/// </summary>
	/// <param name="mode">The ID of the target ambient mode.</param>
	/// <returns>
	///     The ID of the selected scene available, or the current scene if
	///     the target scene isn't found.
	/// </returns>
	[HttpPost("ambientScene")]
	public async Task<ActionResult<AmbientScene>?> SetAmbientScene([FromBody] int mode) {
		var sd = DataUtil.GetSystemData();
		var storeD = new StoreData();
		var scenes = storeD.AmbientScenes;
		foreach (var scene in scenes) {
			if (scene.Id != mode) {
				continue;
			}

			sd.AmbientScene = mode;
			sd.DeviceMode = DeviceMode.Ambient;
			await _controlService.UpdateSystem(sd);
			return scene;
		}

		return null;
	}

	/// <summary>
	///     Get the currently selected audio scene.
	/// </summary>
	/// <returns></returns>
	[HttpGet("audioScene")]
	public ActionResult<AudioScene>? GetAudioScene() {
		var sd = DataUtil.GetSystemData();
		var sData = new StoreData();
		var scenes = sData.AudioScenes;
		foreach (var aScene in scenes) {
			if (aScene.Id == sd.AudioScene) {
				return aScene;
			}
		}

		return null;
	}

	/// <summary>
	///     Set the desired audio scene and set device mode to Audio.
	/// </summary>
	/// <param name="mode">ID of the target audio scene.</param>
	/// <returns></returns>
	[HttpPost("audioScene")]
	public async Task<ActionResult<AudioScene>?> SetAudioScene([FromBody] int mode) {
		var sd = DataUtil.GetSystemData();
		var storeD = new StoreData();
		var scenes = storeD.AudioScenes;
		foreach (var scene in scenes) {
			if (scene.Id != mode) {
				continue;
			}

			sd.AudioScene = mode;
			sd.DeviceMode = DeviceMode.Audio;
			await _controlService.UpdateSystem(sd);
			return scene;
		}

		return null;
	}

	/// <summary>
	///     List the available audio visualizer effect modes.
	/// </summary>
	[HttpGet("audioModes")]
	public ActionResult<string[]> GetAudioModes() {
		return AudioEffectRegistry.AvailableModes
			.Distinct()
			.OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
			.ToArray();
	}

	/// <summary>
	///     Retrieve an array of available audio scenes.
	/// </summary>
	/// <returns></returns>
	[HttpGet("audioScenes")]
	public ActionResult<AudioScene[]> GetAudioScenes() {
		return new StoreData().AudioScenes;
	}

	/// <summary>
	///     List all scenes (scripted animations + baked video sequences).
	/// </summary>
	[HttpGet("scenes")]
	public ActionResult<SceneV2[]> GetScenes() {
		return new SceneStore().List().ToArray();
	}

	/// <summary>
	///     Get a single scene by id.
	/// </summary>
	[HttpGet("scene")]
	public ActionResult<SceneV2> GetScene([FromQuery] string id) {
		var scene = new SceneStore().Get(id);
		return scene == null ? NotFound() : new ActionResult<SceneV2>(scene);
	}

	/// <summary>
	///     Create or update a (script) scene. For script scenes the DSL is compiled to populate
	///     the grid size / fps and to surface any errors.
	/// </summary>
	[HttpPost("scene")]
	public async Task<ActionResult<SceneV2>> SaveScene([FromBody] SceneV2 scene) {
		if (scene == null) {
			return BadRequest("No scene provided.");
		}

		var store = new SceneStore();
		var existing = string.IsNullOrEmpty(scene.Id) ? null : store.Get(scene.Id);
		if (existing is { System: true }) {
			return BadRequest("System scenes cannot be modified.");
		}

		if (string.Equals(scene.Kind, "script", StringComparison.OrdinalIgnoreCase) ||
		    string.IsNullOrWhiteSpace(scene.Kind)) {
			scene.Kind = "script";
			// Substitute declared params with their defaults so authored ${tokens} compile.
			var script = SceneParams.Apply(scene.Script, SceneParams.Resolve(scene, null));
			var spec = SceneScriptParser.Parse(script, out var errors);
			// Author-controlled grid/fps from the script is authoritative.
			scene.Width = spec.Width;
			scene.Height = spec.Height;
			scene.Fps = spec.Fps;
			if (errors.Count > 0) {
				return BadRequest(new { ok = false, errors });
			}
		}

		var saved = store.Save(scene);
		await _controlService.NotifyClients();
		return new ActionResult<SceneV2>(saved);
	}

	/// <summary>
	///     Validate (compile) an animation script without saving it.
	/// </summary>
	[HttpPost("scene/validate")]
	public ActionResult ValidateScene([FromBody] string script) {
		SceneScriptParser.Parse(script ?? string.Empty, out var errors);
		return Ok(new { ok = errors.Count == 0, errors });
	}

	/// <summary>
	///     Activate a scene: set it as current and switch device mode to Scene.
	/// </summary>
	[HttpPost("scene/select")]
	public async Task<IActionResult> SelectScene([FromBody] string id) {
		var store = new SceneStore();
		if (store.Get(id) == null) {
			return NotFound();
		}

		var sd = DataUtil.GetSystemData();
		sd.SceneId = id;
		sd.DeviceMode = DeviceMode.Scene;
		await _controlService.UpdateSystem(sd);
		await _controlService.SetMode(DeviceMode.Scene);
		return Ok(true);
	}

	/// <summary>
	///     Delete a (user) scene and its frame blob.
	/// </summary>
	[HttpDelete("scene")]
	public async Task<IActionResult> DeleteScene([FromQuery] string id) {
		var ok = new SceneStore().Delete(id);
		if (ok) {
			await _controlService.NotifyClients();
		}

		return Ok(ok);
	}

	/// <summary>
	///     Upload a PNG/JPEG data URL to use as a scene thumbnail.
	/// </summary>
	[HttpPost("scene/thumbnail")]
	public async Task<IActionResult> SaveSceneThumbnail([FromBody] SceneThumbnailRequest req) {
		if (req == null || string.IsNullOrWhiteSpace(req.SceneId) || string.IsNullOrWhiteSpace(req.DataUrl)) {
			return BadRequest("SceneId and DataUrl are required.");
		}

		var store = new SceneStore();
		var scene = store.Get(req.SceneId);
		if (scene == null) {
			return NotFound();
		}

		try {
			var comma = req.DataUrl.IndexOf(',');
			if (comma < 0 || comma + 1 >= req.DataUrl.Length) {
				return BadRequest("Invalid image payload.");
			}

			var header = req.DataUrl[..comma];
			var payload = req.DataUrl[(comma + 1)..];
			var ext = header.Contains("image/jpeg", StringComparison.OrdinalIgnoreCase) ? ".jpg" : ".png";
			var bytes = Convert.FromBase64String(payload);

			scene.ThumbnailFile = scene.Id + ".thumb" + ext;
			System.IO.File.WriteAllBytes(store.ThumbnailPath(scene), bytes);
			store.Save(scene);
			await _controlService.NotifyClients();
			return Ok(new { ok = true, scene.ThumbnailFile });
		} catch (Exception e) {
			Log.Warning("Failed to save scene thumbnail: " + e.Message);
			return BadRequest("Unable to save thumbnail.");
		}
	}

	/// <summary>
	///     Capture the current live output preview and store it as the scene thumbnail.
	/// </summary>
	[HttpPost("scene/thumbnail/capture")]
	public async Task<IActionResult> CaptureSceneThumbnail([FromBody] SceneThumbnailCaptureRequest req) {
		if (req == null || string.IsNullOrWhiteSpace(req.SceneId)) {
			return BadRequest("SceneId is required.");
		}

		var store = new SceneStore();
		var scene = store.Get(req.SceneId);
		if (scene == null) {
			return NotFound();
		}

		// Give the renderer a short window to produce an output preview frame.
		byte[]? png = null;
		for (var i = 0; i < 20; i++) {
			png = ControlService.GetLastPreviewImage("outputImage");
			if (png is { Length: > 0 }) {
				break;
			}

			await Task.Delay(250);
		}

		if (png is not { Length: > 0 }) {
			return BadRequest("No preview frame available yet.");
		}

		try {
			scene.ThumbnailFile = scene.Id + ".thumb.png";
			System.IO.File.WriteAllBytes(store.ThumbnailPath(scene), png);
			store.Save(scene);
			await _controlService.NotifyClients();
			return Ok(new { ok = true, scene.ThumbnailFile });
		} catch (Exception e) {
			Log.Warning("Failed to capture scene thumbnail: " + e.Message);
			return BadRequest("Unable to capture thumbnail.");
		}
	}

	/// <summary>
	///     Fetch a scene thumbnail image if one exists.
	/// </summary>
	[HttpGet("scene/thumbnail")]
	public IActionResult GetSceneThumbnail([FromQuery] string id) {
		var store = new SceneStore();
		var scene = store.Get(id);
		if (scene == null || string.IsNullOrWhiteSpace(scene.ThumbnailFile)) {
			return NotFound();
		}

		var path = store.ThumbnailPath(scene);
		if (!System.IO.File.Exists(path)) {
			return NotFound();
		}

		var ext = Path.GetExtension(path).ToLowerInvariant();
		var contentType = ext == ".jpg" || ext == ".jpeg" ? "image/jpeg" : "image/png";
		return PhysicalFile(path, contentType);
	}

	/// <summary>
	///     Force preview streaming on/off without requiring a SignalR SettingsShown client.
	/// </summary>
	[HttpPost("previewActive")]
	public IActionResult SetPreviewActive([FromBody] bool active) {
		_controlService.SendPreview = active;
		return Ok(new { active });
	}

	/// <summary>
	///     Bake an uploaded video into a scene (server-side decode + downsample).
	/// </summary>
	[HttpPost("scene/bakeVideo")]
	[RequestSizeLimit(1_073_741_824)]
	public async Task<IActionResult> BakeVideo(
		IFormFile file,
		[FromForm] string? name,
		[FromForm] int width,
		[FromForm] int height,
		[FromForm] double fps,
		[FromForm] double trimStart,
		[FromForm] double trimEnd,
		[FromForm] double speed,
		[FromForm] string? fit,
		[FromForm] bool loop,
		[FromForm] bool reverse,
		[FromForm] bool mirror) {
		if (file == null || file.Length == 0) {
			return BadRequest("No video uploaded.");
		}

		var tempPath = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName));
		try {
			await using (var stream = new FileStream(tempPath, FileMode.Create)) {
				await file.CopyToAsync(stream);
			}

			var store = new SceneStore();
			var baker = new VideoBaker(store);
			var scene = baker.Bake(tempPath, new VideoBaker.Options {
				Name = name ?? "Video Scene",
				Width = width,
				Height = height,
				Fps = fps,
				TrimStart = trimStart,
				TrimEnd = trimEnd,
				Speed = speed,
				Fit = string.IsNullOrWhiteSpace(fit) ? "crop" : fit,
				Loop = loop,
				Reverse = reverse,
				Mirror = mirror
			});
			await _controlService.NotifyClients();
			return Ok(scene);
		} catch (Exception e) {
			Log.Warning("Video bake failed: " + e.Message);
			return BadRequest(e.Message);
		} finally {
			try {
				if (System.IO.File.Exists(tempPath)) {
					System.IO.File.Delete(tempPath);
				}
			} catch (Exception) {
				// ignored
			}
		}
	}


	/// <summary>
	///     Trigger device authorization for the specified device.
	/// </summary>
	/// <param name="id">The device Id to try authorizing.</param>
	/// <returns>True or false representing if the device is authorized.</returns>
	[HttpGet("authorizeDevice")]
	public async Task<ActionResult<bool>> AuthorizeDevice([FromQuery] string id) {
		var result = await _controlService.AuthorizeDevice(id);
		return result;
	}

	/// <summary>
	///     Arm the emulated Hue Bridge "link button" so the Hue Sync app can pair with Glimmr.
	///     Call this, then press "connect/search" in the Hue Sync app within the window.
	/// </summary>
	/// <returns>An object describing the current pairing state.</returns>
	[HttpGet("hueSyncLink")]
	public ActionResult<object> HueSyncLink() {
		var bridge = Models.ColorSource.HueSync.HueSyncBridge.Instance;
		bridge.ArmLink();
		return new {
			bridge.BridgeId,
			bridge.BridgeMac,
			armed = bridge.IsLinkArmed,
			paired = bridge.IsPaired
		};
	}

	/// <summary>
	///     Report whether the Hue Sync app is currently paired with the emulated bridge.
	/// </summary>
	[HttpGet("hueSyncStatus")]
	public ActionResult<object> HueSyncStatus() {
		var bridge = Models.ColorSource.HueSync.HueSyncBridge.Instance;
		return new {
			bridge.BridgeId,
			bridge.BridgeMac,
			armed = bridge.IsLinkArmed,
			paired = bridge.IsPaired,
			streaming = bridge.Streaming
		};
	}

	/// <summary>
	///     Forget the paired Hue Sync app, forcing a fresh pairing next time.
	/// </summary>
	[HttpGet("hueSyncUnpair")]
	public ActionResult<bool> HueSyncUnpair() {
		Models.ColorSource.HueSync.HueSyncBridge.Instance.Unpair();
		return true;
	}

	/// <summary>
	///     Download a backup of the current database.
	/// </summary>
	/// <returns>A copy of the LiteDB used for setting storage.</returns>
	[HttpGet("database")]
	public FileResult DbDownload() {
		Log.Debug("DB Download requested...");
		var dbPath = DataUtil.BackupDb();
		var fileBytes = System.IO.File.ReadAllBytes(dbPath);
		var fileName = Path.GetFileName(dbPath);
		Log.Debug("DB path: " + dbPath);
		return File(fileBytes, MediaTypeNames.Application.Octet, fileName);
	}

	/// <summary>
	///     Upload and replace the database with a copy from a db download.
	/// </summary>
	/// <param name="files">Technically a file list, but in reality, just one LiteDB file.</param>
	/// <returns>True if the import succeeded, false if not.</returns>
	[HttpPost("database")]
	public async Task<IActionResult> ImportDb(List<IFormFile> files) {
		var filePaths = new List<string>();
		foreach (var formFile in files) {
			if (formFile.Length <= 0) {
				continue;
			}

			// full path to file in temp location
			var filePath =
				Path.GetTempFileName(); //we are using Temp file name just for the example. Add your own file path.
			filePaths.Add(filePath);
			await using var stream = new FileStream(filePath, FileMode.Create);
			await formFile.CopyToAsync(stream);
		}

		var imported = false;
		if (filePaths.Count != 1) {
			Log.Warning("Error, we should only have one DB file!");
		} else {
			if (DataUtil.ImportSettings(filePaths[0])) {
				Log.Information("Import was successful!");
				imported = true;
			} else {
				Log.Warning("Import failed!!");
			}
		}

		if (imported) {
			await _controlService.NotifyClients();
		}

		return Ok(imported);
	}


	/// <summary>
	///     Retrieve the entire datastore in JSON format.
	/// </summary>
	/// <returns>a JSON representation of the entire database.</returns>
	[HttpGet("databaseJson")]
	public ActionResult<StoreData> LoadData() {
		return new ActionResult<StoreData>(DataUtil.GetStoreSerialized(_controlService));
	}

	/// <summary>
	///     Retrieve target device data.
	/// </summary>
	/// <param name="id">The id of the device to retrieve.</param>
	/// <returns>Device data or null if not found.</returns>
	[HttpGet("device")]
	public ActionResult<IColorTargetData> GetDevice([FromQuery] string id) {
		var devs = DataUtil.GetDevices();
		foreach (var device in devs.Select(dev => (IColorTargetData)dev).Where(device => device.Id == id)) {
			return new ActionResult<IColorTargetData>(device);
		}

		return NotFound($"Device {id} not found.");
	}

	/// <summary>
	///     Insert or update a device.
	/// </summary>
	/// <param name="dData">A JSON string representing the ColorTarget to update.</param>
	/// <returns>The updated ColorTarget object.</returns>
	[HttpPost("device")]
	public async Task<ActionResult<IColorTargetData>> UpdateDevice([FromBody] IColorTargetData dData) {
		await _controlService.UpdateDevice(dData, false);
		return new ActionResult<IColorTargetData>(dData);
	}

	/// <summary>
	///     Delete a device.
	/// </summary>
	/// <param name="id">The ID of the device to delete.</param>
	/// <returns>The updated ColorTarget object.</returns>
	[HttpDelete("device")]
	public async Task<ActionResult<bool>> DeleteDevice(string id) {
		var res = await _controlService.RemoveDevice(id);
		return new ActionResult<bool>(res);
	}

	/// <summary>
	///     Retrieve the current list of devices.
	/// </summary>
	/// <returns></returns>
	[HttpGet("devices")]
	public ActionResult<IColorTargetData[]> GetDevices() {
		var devs = DataUtil.GetDevices();
		return new ActionResult<IColorTargetData[]>(devs.Select(dev => (IColorTargetData)dev).ToArray());
	}

	/// <summary>
	///     Flash an entire device.
	/// </summary>
	/// <param name="deviceId">The ID of the device to flash on/off.</param>
	/// <returns></returns>
	// POST: api/DreamData/flashDevice
	[HttpPost("flashDevice")]
	public async Task<IActionResult> FlashDevice([FromBody] string deviceId) {
		await _controlService.FlashDevice(deviceId);
		return Ok();
	}


	/// <summary>
	///     Flash a specific LED from the grid.
	/// </summary>
	/// <param name="len">The LED ID to flash.</param>
	/// <returns></returns>
	[HttpPost("flashLed")]
	public async Task<IActionResult> TestStripOffset([FromBody] int len) {
		await _controlService.FlashLed(len);
		return Ok();
	}

	/// <summary>
	///     Flash a specific Sector.
	/// </summary>
	/// <param name="sector">The sector ID to flash.</param>
	/// <returns></returns>
	// POST: api/DreamData/flashSector
	[HttpPost("flashSector")]
	public async Task<IActionResult> FlashSector([FromBody] int sector) {
		await _controlService.FlashSector(sector);
		return Ok(sector);
	}

	/// <summary>
	///     Retrieves a simplified version of our SystemData object used for Glimmr-to-Glimmr control.
	/// </summary>
	/// <returns>
	///     <see cref="GlimmrData" />
	/// </returns>
	[HttpGet("glimmrData")]
	public ActionResult<GlimmrData> GetJson() {
		var sd = DataUtil.GetSystemData();
		var glimmrData = new GlimmrData(sd);
		return new ActionResult<GlimmrData>(glimmrData);
	}

	/// <summary>
	///     Upload and replace the database with a copy from a db download.
	/// </summary>
	/// <param name="files">Technically a file list, but in reality, just one LiteDB file.</param>
	/// <returns>True if the import succeeded, false if not.</returns>
	[HttpPost("importAmbientScene")]
	public async Task<IActionResult> ImportAmbient(List<IFormFile> files) {
		var filePaths = new List<string>();
		foreach (var formFile in files) {
			if (formFile.Length <= 0) {
				continue;
			}

			// full path to file in temp location
			var filePath =
				Path.GetTempFileName(); //we are using Temp file name just for the example. Add your own file path.
			filePaths.Add(filePath);
			await using var stream = new FileStream(filePath, FileMode.Create);
			await formFile.CopyToAsync(stream);
		}

		var imported = false;
		foreach (var path in filePaths) {
			try {
				var loader = new JsonLoader("ambientScenes");
				imported = loader.ImportJson(path);
			} catch (Exception) {
				imported = false;
			}
		}

		if (imported) {
			await _controlService.NotifyClients();
		}

		return Ok(imported);
	}

	/// <summary>
	///     Upload and replace the database with a copy from a db download.
	/// </summary>
	/// <param name="files">Technically a file list, but in reality, just one LiteDB file.</param>
	/// <returns>True if the import succeeded, false if not.</returns>
	[HttpPost("importAudioScene")]
	public async Task<IActionResult> ImportAudio(List<IFormFile> files) {
		var filePaths = new List<string>();
		foreach (var formFile in files) {
			if (formFile.Length <= 0) {
				continue;
			}

			// full path to file in temp location
			var filePath =
				Path.GetTempFileName(); //we are using Temp file name just for the example. Add your own file path.
			filePaths.Add(filePath);
			await using var stream = new FileStream(filePath, FileMode.Create);
			await formFile.CopyToAsync(stream);
		}

		var imported = false;
		foreach (var path in filePaths) {
			try {
				var loader = new JsonLoader("audioScenes");
				imported = loader.ImportJson(path);
			} catch (Exception) {
				imported = false;
			}
		}

		if (imported) {
			await _controlService.NotifyClients();
		}

		return Ok(imported);
	}

	/// <summary>
	///     Download the current log file.
	/// </summary>
	/// <returns>A plain-text logfile.</returns>
	[HttpGet("logs")]
	public async Task<FileResult> LogDownload() {
		var dt = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
		var logPath = $"/var/log/glimmr/glimmr{dt}.log";
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
			var userPath = SystemUtil.GetUserDir();
			var logDir = Path.Combine(userPath, "log");
			if (!Directory.Exists(logDir)) {
				Directory.CreateDirectory(logDir);
			}

			logPath = Path.Combine(userPath, "log", $"glimmr{dt}.log");
		}

		byte[] result;
		await using (FileStream stream = new(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
			result = new byte[stream.Length];
			var readAsync = await stream.ReadAsync(result.AsMemory(0, (int)stream.Length));
			Log.Debug("Read " + readAsync + " bytes of log data.");
		}

		return File(result, MediaTypeNames.Application.Octet, $"glimmr{dt}.log");
	}


	/// <summary>
	///     Retrieve or set the current device mode.
	/// </summary>
	/// <param name="mode">If specified, set the mode to the value.</param>
	/// <returns>The current device mode.</returns>
	[HttpGet("mode")]
	public async Task<ActionResult<DeviceMode>> Mode(int mode = -1) {
		if (mode != -1) {
			await _controlService.SetMode((DeviceMode)mode);
		}

		var sd = DataUtil.GetSystemData();
		return new ActionResult<DeviceMode>(sd.DeviceMode);
	}

	/// <summary>
	///     Set a new device mode.
	/// </summary>
	/// ///
	/// <remarks>
	///     Device Modes:
	///     Off = 0,
	///     Video = 1,
	///     Audio = 2,
	///     AudioVideo = 4,
	///     Ambient = 3,
	///     Udp = 5,
	///     DreamScreen = 6
	/// </remarks>
	/// <param name="mode">The new device mode to set.</param>
	/// <returns>The newly-set mode.</returns>
	// POST: api/DreamData/mode
	[HttpPost("mode")]
	public async Task<ActionResult<DeviceMode>> DevMode([FromBody] DeviceMode mode) {
		await _controlService.SetMode(mode);
		return new ActionResult<DeviceMode>(mode);
	}

	/// <summary>
	///     Triggers a device refresh.
	/// </summary>
	/// <returns>A List of devices.</returns>
	// GET: api/DreamData/refreshDevices
	[HttpPost("scanDevices")]
	public async Task<ActionResult<IColorTargetData[]>> ScanDevices() {
		await _controlService.ScanDevices();
		await Task.Delay(TimeSpan.FromSeconds(5));
		return new ActionResult<IColorTargetData[]>(
			DataUtil.GetDevices().Select(i => (IColorTargetData)i).ToArray());
	}

	/// <summary>
	///     Fetch current CPU statistics.
	/// </summary>
	/// <returns></returns>
	[HttpGet("stats")]
	public async Task<ActionResult<StatData>> GetStats() {
		return await StatUtil.GetStats();
	}

	/// <summary>
	///     Fetch the current performance counters tracked by the runtime pipeline.
	/// </summary>
	[HttpGet("stats/perf")]
	public ActionResult<PerformanceData> GetPerfStats() {
		return new ActionResult<PerformanceData>(_controlService.ColorService.SnapshotPerformance());
	}

	/// <summary>
	///     Reset runtime performance counters used for benchmarking/audit passes.
	/// </summary>
	[HttpPost("stats/perf/reset")]
	public IActionResult ResetPerfStats() {
		_controlService.ColorService.ResetPerformanceCounters();
		return Ok(new { ok = true });
	}


	/// <summary>
	///     Start Glimmr-to-Glimmr UDP stream
	/// </summary>
	/// <param name="gd">
	///     A GlimmrData object containing the input dimensions of
	///     the received colors.
	/// </param>
	/// <returns></returns>
	[HttpPost("startStream")]
	public async Task<ActionResult<bool>> StartStream([FromBody] GlimmrData gd) {
		await _controlService.StartStream(gd);
		return new ActionResult<bool>(true);
	}

	/// <summary>
	///     Accept Monitorr realtime LED color frames over a LAN WebSocket.
	/// </summary>
	/// <returns></returns>
	[HttpGet("realtime")]
	public async Task<IActionResult> Realtime(CancellationToken cancellationToken) {
		if (!HttpContext.WebSockets.IsWebSocketRequest) {
			return BadRequest("Monitorr realtime requires a WebSocket request.");
		}

		var colorService = _controlService.ColorService;
		using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();
		using var session = new MonitorrRealtimeSession(colorService);
		await session.Run(socket, cancellationToken).ConfigureAwait(false);
		return new EmptyResult();
	}

	/// <summary>
	///     Fetch store data object.
	/// </summary>
	/// <returns></returns>
	[HttpGet("store")]
	public ActionResult<StoreData> GetStore() {
		return new ActionResult<StoreData>(DataUtil.GetStoreSerialized(_controlService));
	}

	/// <summary>
	///     Triggers a system action.
	/// </summary>
	/// <param name="action">
	///     Available commands are "restart", "shutdown", "reboot", and "update".
	///     Restart restarts ONLY the glimmr service.
	///     Shutdown shuts down the entire machine.
	///     Reboot triggers a system reboot.
	///     Update will stop Glimmr, update the software, and re-start the service.
	/// </param>
	/// <returns></returns>
	[HttpPost("systemControl")]
	public IActionResult SysControl([FromBody] string action) {
		ControlService.SystemControl(action);
		return Ok(action);
	}

	/// <summary>
	///     Update System configuration.
	/// </summary>
	/// <param name="ld">A SystemData object.</param>
	/// <returns>The updated SystemData object.</returns>
	// POST: api/DreamData/ledData
	[HttpPost("systemData")]
	public async Task<ActionResult<SystemData>> UpdateSystem([FromBody] SystemData ld) {
		await _controlService.UpdateSystem(ld);
		return new ActionResult<SystemData>(ld);
	}

	/// <summary>
	///     Export the full configuration as a versioned JSON bundle.
	/// </summary>
	/// <returns>An ExportBundle containing all devices, scenes, and settings.</returns>
	[HttpGet("export")]
	public ActionResult<ExportBundle> Export() {
		try {
			var svc = new BackupService(_controlService);
			var bundle = svc.CreateExport();
			return new ActionResult<ExportBundle>(bundle);
		} catch (Exception e) {
			Log.Warning("Export failed: " + e.Message);
			return StatusCode(500, "Export failed: " + e.Message);
		}
	}

	/// <summary>
	///     Import a previously exported configuration bundle.
	/// </summary>
	/// <param name="bundle">The ExportBundle to import.</param>
	/// <returns>An ImportResult with migration details and any warnings.</returns>
	[HttpPost("import")]
	public async Task<ActionResult<ImportResult>> Import([FromBody] ExportBundle bundle) {
		try {
			var svc = new BackupService(_controlService);
			var result = svc.ImportExport(bundle);
			if (result.Success) {
				await _controlService.NotifyClients();
			}

			return new ActionResult<ImportResult>(result);
		} catch (Exception e) {
			Log.Warning("Import failed: " + e.Message);
			return StatusCode(500, "Import failed: " + e.Message);
		}
	}

	/// <summary>
	///     Download the full configuration export as a .json file.
	/// </summary>
	/// <returns>A downloadable JSON file containing the ExportBundle.</returns>
	[HttpGet("export/download")]
	public IActionResult ExportDownload() {
		try {
			var svc = new BackupService(_controlService);
			var path = svc.ExportToFile();
			var fileBytes = System.IO.File.ReadAllBytes(path);
			var fileName = Path.GetFileName(path);
			return File(fileBytes, MediaTypeNames.Application.Octet, fileName);
		} catch (Exception e) {
			Log.Warning("Export download failed: " + e.Message);
			return StatusCode(500, "Export download failed: " + e.Message);
		}
	}

	/// <summary>
	///     Retrieve only the export manifest for a lightweight compatibility check.
	/// </summary>
	/// <returns>An ExportManifest summarizing what would be exported.</returns>
	[HttpGet("export/manifest")]
	public ActionResult<ExportManifest> ExportManifest() {
		try {
			var svc = new BackupService(_controlService);
			var bundle = svc.CreateExport();
			return new ActionResult<ExportManifest>(bundle.Manifest);
		} catch (Exception e) {
			Log.Warning("Export manifest failed: " + e.Message);
			return StatusCode(500, "Export manifest failed: " + e.Message);
		}
	}

	/// <summary>
	///     Generate a support diagnostics package with anonymized config, logs,
	///     network info, and performance metrics.
	/// </summary>
	/// <returns>A SupportPackage for troubleshooting.</returns>
	[HttpGet("support")]
	public ActionResult<SupportPackage> GetSupportPackage() {
		try {
			var svc = new BackupService(_controlService);
			var package = svc.GenerateSupportPackage();
			return new ActionResult<SupportPackage>(package);
		} catch (Exception e) {
			Log.Warning("Support package generation failed: " + e.Message);
			return StatusCode(500, "Support package generation failed: " + e.Message);
		}
	}
}

public sealed class SceneThumbnailRequest {
	public string SceneId { get; init; } = "";
	public string DataUrl { get; init; } = "";
}

public sealed class SceneThumbnailCaptureRequest {
	public string SceneId { get; init; } = "";
}