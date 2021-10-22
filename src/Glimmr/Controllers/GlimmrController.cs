#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models;
using Glimmr.Models.ColorSource.Ambient;
using Glimmr.Models.ColorSource.Audio;
using Glimmr.Models.ColorTarget;
using Glimmr.Models.ColorTarget.Glimmr;
using Glimmr.Models.Util;
using Glimmr.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Serilog;
using DeviceMode = Glimmr.Enums.DeviceMode;

#endregion

namespace Glimmr.Controllers {
	[Route("api/[controller]")]
	[ApiController]
	public class GlimmrController : ControllerBase {
		private readonly ControlService _controlService;

		public GlimmrController(ControlService controlService) {
			_controlService = controlService;
		}

		/// <summary>
		/// Default endpoint - returns a SystemData object.
		/// </summary>
		/// <returns><see cref="SystemData"/></returns>
		[HttpGet("")]
		public ActionResult<SystemData> GetSystemData() {
			var sd = DataUtil.GetSystemData();
			return sd;
		}
		
		/// <summary>
		/// Retrieve the currently set ambient color.
		/// Will still return a value if ambient mode is not
		/// active.
		/// </summary>
		/// <returns>A HTML RGB hex string (#FFFFFF).</returns>
		[HttpGet("ambientColor")]
		public ActionResult<string> GetAmbientColor() {
			var sd = DataUtil.GetSystemData();
			return sd.AmbientColor;
		}

		
		/// <summary>
		/// Set the current ambient mode, and set device mode to ambient.
		/// </summary>
		/// <param name="color">A HTML-formatted RGB Color (#666666/FFFFFF)</param>
		/// <returns>The ID of the selected scene available, or -1 if not found.</returns>
		[HttpPost("ambientColor")]
		public async Task<ActionResult<string>> SetAmbientColor([FromBody]string color) {
			Log.Debug("String: " + color);
			var sd = DataUtil.GetSystemData();
			if (color[..1] != "#" && color.Length == 6) {
				color = "#" + color;
			}
			if (!Regex.Match(color, "^#(?:[0-9a-fA-F]{3}){1,2}$").Success) {
				return Ok(sd.AmbientColor);
			}

			sd.AmbientColor = color.Replace("#","");
			sd.AmbientScene = -1;
			sd.DeviceMode = DeviceMode.Ambient;
			await _controlService.UpdateSystem(sd);
			return Ok(sd.AmbientColor);
		}

		/// <summary>
		/// Retrieve the ambient scene specified in the query, or current if none is specified.
		/// Will still return a value if ambient mode is not
		/// active.
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
		/// Retrieve the list of available ambient scenes.
		/// </summary>
		/// <returns>An array of ambient scenes.</returns>
		[HttpGet("ambientScenes")]
		public ActionResult<AmbientScene[]> GetAmbientScenes() {
			var sd = new StoreData();
			return sd.AmbientScenes;
		}
		
		/// <summary>
		/// Set the current ambient mode, and set device mode to ambient.
		/// </summary>
		/// <param name="mode">The ID of the target ambient mode.</param>
		/// <returns>The ID of the selected scene available, or the current scene if
		/// the target scene isn't found.</returns>
		[HttpPost("ambientScene")]
		public async Task<ActionResult<AmbientScene>?> SetAmbientScene([FromBody]int mode) {
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
		/// Get the currently selected audio scene.
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
		/// Set the desired audio scene and set device mode to Audio.
		/// </summary>
		/// <param name="mode">ID of the target audio scene.</param>
		/// <returns></returns>
		[HttpPost("audioScene")]

		public async Task<ActionResult<AudioScene>?> SetAudioScene([FromBody]int mode) {
			var sd = DataUtil.GetSystemData();
			var storeD = new StoreData();
			var scenes = storeD.AudioScenes;
			foreach (var scene in scenes) {
				if (scene.Id != mode) {
					continue;
				}

				sd.AmbientScene = mode;
				sd.DeviceMode = DeviceMode.Audio;
				await _controlService.UpdateSystem(sd);
				return scene;
			}

			return null;
		}

		/// <summary>
		/// Retrieve an array of available audio scenes.
		/// </summary>
		/// <returns></returns>
		[HttpGet("audioScenes")]
		public ActionResult<AudioScene[]> GetAudioScenes() {
			return new StoreData().AudioScenes;
		}

		
		/// <summary>
		/// Trigger device authorization for the specified device.
		/// </summary>
		/// <param name="id">The device Id to try authorizing.</param>
		/// <returns>True or false representing if the device is authorized.</returns>
		[HttpGet("authorizeDevice")]
		public async Task<ActionResult<bool>> AuthorizeDevice([FromQuery] string id) {
			var result = await _controlService.AuthorizeDevice(id);
			return result;
		}
		
		/// <summary>
		/// Download a backup of the current database.
		/// </summary>
		/// <returns>A copy of the LiteDB used for setting storage.</returns>
		[HttpGet("database")]
		public FileResult DbDownload() {
			var dbPath = DataUtil.BackupDb();
			Log.Debug("Fetching DB from: " + dbPath);
			var fileBytes = System.IO.File.ReadAllBytes(dbPath);
			var fileName = Path.GetFileName(dbPath);
			return File(fileBytes, MediaTypeNames.Application.Octet, fileName);
		}
		
		/// <summary>
		/// Upload and replace the database with a copy from a db download.
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
				Log.Debug("We have a db, restoring.");
				if (DataUtil.ImportSettings(filePaths[0])) {
					Log.Debug("Import was successful!");
					imported = true;
				} else {
					Log.Debug("Import failed!!");
				}
			}

			if (imported) {
				await _controlService.NotifyClients();
			}
			return Ok(imported);
		}


		/// <summary>
		/// Retrieve the entire datastore in JSON format.
		/// </summary>
		/// <returns>a JSON representation of the entire database.</returns>
		[HttpGet("databaseJson")]
		public ActionResult<StoreData> LoadData() {
			return new ActionResult<StoreData>(DataUtil.GetStoreSerialized(_controlService));
		}

		/// <summary>
		/// Retrieve target device data.
		/// </summary>
		/// <param name="id">The id of the device to retrieve.</param>
		/// <returns>Device data or null if not found.</returns>
		[HttpGet("device")]
		public ActionResult<IColorTargetData> GetDevice([FromQuery]string id) {
			var devs = DataUtil.GetDevices();
			foreach (var device in devs.Select(dev => (IColorTargetData) dev).Where(device => device.Id == id)) {
				return new ActionResult<IColorTargetData>(device);
			}
			return NotFound($"Device {id} not found.");
		}
		
		/// <summary>
		/// Insert or update a device.
		/// </summary>
		/// <param name="dData">A JSON string representing the ColorTarget to update.</param>
		/// <returns>The updated ColorTarget object.</returns>
		[HttpPost("device")]
		public async Task<ActionResult<IColorTargetData>> UpdateDevice([FromBody] IColorTargetData dData) {
			Log.Debug("Update device fired: " + JsonConvert.SerializeObject(dData));
			await _controlService.UpdateDevice(dData, false);
			return new ActionResult<IColorTargetData>(dData);
		}

		/// <summary>
		/// Delete a device.
		/// </summary>
		/// <param name="id">The ID of the device to delete.</param>
		/// <returns>The updated ColorTarget object.</returns>
		[HttpDelete("device")]
		public async Task<ActionResult<bool>> DeleteDevice(string id) {
			Log.Debug("Delete device fired: " + id);
			var res = await _controlService.RemoveDevice(id);
			return new ActionResult<bool>(res);
		}
		
		/// <summary>
		/// Retrieve the current list of devices.
		/// </summary>
		/// <returns></returns>
		[HttpGet("devices")]
		public ActionResult<IColorTargetData[]> GetDevices() {
			var devs = DataUtil.GetDevices();
			return new ActionResult<IColorTargetData[]>(devs.Select(dev => (IColorTargetData)dev).ToArray());
		}
		
		/// <summary>
		/// Flash an entire device.
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
		/// Flash a specific LED from the grid.
		/// </summary>
		/// <param name="len">The LED ID to flash.</param>
		/// <returns></returns>
		[HttpPost("flashLed")]
		public async Task<IActionResult> TestStripOffset([FromBody] int len) {
			Log.Debug("Get got: " + len);
			await _controlService.TestLights(len);
			return Ok();
		}

		/// <summary>
		/// Flash a specific Sector.
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
		/// Retrieves a simplified version of our SystemData object used for Glimmr-to-Glimmr control.
		/// </summary>
		/// <returns><see cref="GlimmrData"/></returns>
		[HttpGet("glimmrData")]
		public ActionResult<GlimmrData> GetJson() {
			var sd = DataUtil.GetSystemData();
			var glimmrData = new GlimmrData(sd);
			return new ActionResult<GlimmrData>(glimmrData);
		}
		
		/// <summary>
		/// Upload and replace the database with a copy from a db download.
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
			if (imported) await _controlService.NotifyClients();
			return Ok(imported);
		}
		
		/// <summary>
		/// Upload and replace the database with a copy from a db download.
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
			if (imported) await _controlService.NotifyClients();
			return Ok(imported);
		}
		
		/// <summary>
		/// Download the current log file.
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
				await stream.ReadAsync(result.AsMemory(0, (int)stream.Length));
			}
			return File(result, MediaTypeNames.Application.Octet, $"glimmr{dt}.log");
		}
		
		

		/// <summary>
		/// Retrieve or set the current device mode.
		/// </summary>
		/// <param name="mode">If specified, set the mode to the value.</param>
		/// <returns>The current device mode.</returns>
		[HttpGet("mode")]
		public async Task<ActionResult<DeviceMode>> Mode(int mode=-1) {
			if (mode != -1) {
				Log.Debug("GET request, updating device mode to " + mode);
				await _controlService.SetMode((DeviceMode)mode);
			}
			var sd = DataUtil.GetSystemData();
			return new ActionResult<DeviceMode>(sd.DeviceMode);
		}

		/// <summary>
		/// Set a new device mode.
		/// </summary>
		/// /// <remarks>
		/// Device Modes:
		/// Off = 0,
		/// Video = 1,
		/// Audio = 2,
		/// AudioVideo = 4,
		/// Ambient = 3,
		/// Udp = 5,
		/// DreamScreen = 6
		/// </remarks>
		/// <param name="mode">The new device mode to set.</param>
		/// <returns>The newly-set mode.</returns>
		// POST: api/DreamData/mode
		[HttpPost("mode")]
		public async Task<ActionResult<DeviceMode>> DevMode([FromBody] DeviceMode mode) {
			Log.Debug("Mode set to: " + mode);
			await _controlService.SetMode(mode);
			return new ActionResult<DeviceMode>(mode);
		}

		/// <summary>
		/// Triggers a device refresh.
		/// </summary>
		/// <returns>A List of devices.</returns>
		// GET: api/DreamData/refreshDevices
		[HttpPost("scanDevices")]
		public async Task<ActionResult<IColorTargetData[]>> ScanDevices() {
			await _controlService.ScanDevices();
			Thread.Sleep(5000);
			return new ActionResult<IColorTargetData[]>(DataUtil.GetDevices().Select(i => (IColorTargetData)i).ToArray());
		}

		/// <summary>
		/// Fetch current CPU statistics.
		/// </summary>
		/// <returns></returns>
		[HttpGet("stats")]
		public async Task<ActionResult<StatData>> GetStats() {
			return await CpuUtil.GetStats();
		}

		
		/// <summary>
		/// Start Glimmr-to-Glimmr UDP stream
		/// </summary>
		/// <param name="gd">A GlimmrData object containing the input dimensions of
		/// the received colors.</param>
		/// <returns></returns>
		[HttpPost("startStream")]
		public async Task<ActionResult<bool>> StartStream([FromBody]GlimmrData gd) {
			await _controlService.StartStream(gd);
			return new ActionResult<bool>(true);
		}

		/// <summary>
		/// Triggers a system action.
		/// </summary>
		/// <param name="action">Available commands are "restart", "shutdown", "reboot", and "update".
		///	Restart restarts ONLY the glimmr service.
		/// Shutdown shuts down the entire machine.
		/// Reboot triggers a system reboot.
		/// Update will stop Glimmr, update the software, and re-start the service.
		/// </param>
		/// <returns></returns>
		[HttpPost("systemControl")]
		public IActionResult SysControl([FromBody] string action) {
			ControlService.SystemControl(action);
			return Ok(action);
		}
		
		/// <summary>
		/// Update System configuration.
		/// </summary>
		/// <param name="ld">A SystemData object.</param>
		/// <returns>The updated SystemData object.</returns>
		// POST: api/DreamData/ledData
		[HttpPost("systemData")]
		public async Task<ActionResult<SystemData>> UpdateSystem([FromBody] SystemData ld) {
			await _controlService.UpdateSystem(ld);
			return new ActionResult<SystemData>(ld);
		}
	}
}