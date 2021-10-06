#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Models;
using Glimmr.Models.ColorTarget;
using Glimmr.Models.ColorTarget.Glimmr;
using Glimmr.Models.Util;
using Glimmr.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace Glimmr.Controllers {
	[Route("api/[controller]")]
	[ApiController]
	public class DreamDataController : ControllerBase {
		private readonly ControlService _controlService;

		public DreamDataController(ControlService controlService) {
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
		/// Retrieve the entire datastore in JSON format
		/// </summary>
		/// <returns>a JSON representation of the entire database.</returns>
		[HttpGet("databaseJson")]
		public ActionResult<StoreData> LoadData() {
			return DataUtil.GetStoreSerialized();
		}
		
		/// <summary>
		/// Retrieve the current list of devices
		/// </summary>
		/// <returns></returns>
		[HttpGet("devices")]
		public ActionResult<List<IColorTargetData>> GetDevices() {
			var devs = DataUtil.GetDevices();
			return devs.Select(dev => (IColorTargetData)dev).ToList();
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
		/// Flash a specific LED from the grid
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
		/// Flash a specific Sector
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
		public JsonResult GetJson() {
			var sd = DataUtil.GetSystemData();
			var glimmrData = new GlimmrData(sd);
			return new JsonResult(glimmrData);
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
		/// Retrieve the current device mode
		/// </summary>
		/// <remarks>
		/// Device Modes:
		/// Off = 0,
		/// Video = 1,
		/// Audio = 2,
		/// AudioVideo = 4,
		/// Ambient = 3,
		/// Udp = 5,
		/// DreamScreen = 6
		/// </remarks>
		/// <returns>The current device mode.</returns>
		[HttpGet("mode")]
		public IActionResult Mode() {
			var sd = DataUtil.GetSystemData();
			return Ok((DeviceMode)sd.DeviceMode);
		}

		/// <summary>
		/// Set a new device mode
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
		public async Task<IActionResult> DevMode([FromBody] DeviceMode mode) {
			Log.Debug("Mode set to: " + mode);
			await _controlService.SetMode((int)mode);
			return Ok(mode);
		}

		/// <summary>
		/// Triggers a device refresh
		/// </summary>
		/// <returns>A List of devices.</returns>
		// GET: api/DreamData/refreshDevices
		[HttpPost("scanDevices")]
		public async Task<ActionResult<dynamic[]>> ScanDevices() {
			await _controlService.ScanDevices();
			Thread.Sleep(5000);
			return DataUtil.GetDevices().Select(i=>(IColorTargetData) i).ToArray();
		}

		/// <summary>
		/// Fetch current CPU statistics.
		/// </summary>
		/// <remarks>Example json:
		///{
		///"loadAvg1": 1.23,
		///"loadAvg15": 0.33,
		///"loadAvg5": 0.52,
		///"tempAvg": 146,
		///"tempCurrent": 146,
		///"tempMax": 146,
		///"tempMin": 146,
		///"uptime": "1:12",
		///"throttledState": [
		///"Soft Temperature Limit has occurred"
		///]
		///}
		/// </remarks>
		/// <returns></returns>
		[HttpGet("stats")]
		public IActionResult GetStats() {
			return Ok(CpuUtil.GetStats());
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
		/// Update System configuration
		/// </summary>
		/// <param name="ld">A SystemData object.</param>
		/// <returns>The updated SystemData object.</returns>
		// POST: api/DreamData/ledData
		[HttpPost("systemData")]
		public async Task<ActionResult<SystemData>> UpdateSystem([FromBody] SystemData ld) {
			await _controlService.UpdateSystem(ld);
			return ld;
		}

		/// <summary>
		/// Update a specific Devices data.
		/// </summary>
		/// <param name="dData">A JSON string representing the ColorTarget to update.</param>
		/// <returns>The updated ColorTarget object.</returns>
		// POST: api/DreamData/updateDevice
		[HttpPost("updateDevice")]
		public async Task<ActionResult<IColorTargetData>> UpdateDevice([FromBody] IColorTargetData dData) {
			Log.Debug("Update device fired: " + JsonConvert.SerializeObject(dData));
			await _controlService.UpdateDevice(dData, false);
			return Ok(dData);
		}

		


	}
}