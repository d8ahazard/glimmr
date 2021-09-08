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
using Glimmr.Models;
using Glimmr.Models.ColorTarget.Glimmr;
using Glimmr.Models.Util;
using Glimmr.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

		[HttpGet("")]
		public JsonResult GetSystemData() {
			var sd = DataUtil.GetSystemData();
			return new JsonResult(sd);
		}

		[HttpGet("json")]
		public JsonResult GetJson() {
			var sd = DataUtil.GetSystemData();
			var glimmrData = new GlimmrData(sd);
			return new JsonResult(glimmrData);
		}

		[HttpGet("brightness")]
		public async Task<IActionResult> SetBrightness([FromQuery] int value) {
			Log.Debug("Setting brightness: " + value);
			var sd = DataUtil.GetSystemData();
			sd.Brightness = value;
			await _controlService.UpdateSystem(sd);
			return Ok(value);
		}

		[HttpGet("toggleMode")]
		public async Task<IActionResult> ToggleMode() {
			var sd = DataUtil.GetSystemData();
			var prev = sd.PreviousMode;
			var mode = sd.DeviceMode;
			if (mode == 0) {
				await _controlService.SetMode(prev);
			} else {
				await _controlService.SetMode(0);
			}

			return Ok();
		}

		[HttpGet("DbDownload")]
		public FileResult DbDownload() {
			var dbPath = DataUtil.ExportSettings();
			var fileBytes = System.IO.File.ReadAllBytes(dbPath);
			var fileName = Path.GetFileName(dbPath);
			return File(fileBytes, MediaTypeNames.Application.Octet, fileName);
		}
		
		[HttpGet("LogDownload")]
		public FileResult LogDownload() {
			var dt = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
			var logPath = $"/var/log/glimmr/glimmr{dt}.log";
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				var userPath = SystemUtil.GetUserDir();
				var logDir = Path.Combine(userPath, "log");
				if (!Directory.Exists(logDir)) {
					Directory.CreateDirectory(logDir);
				}

				logPath = Path.Combine(userPath, "log", $"{dt}.log");
			}

			Log.Debug("Grabbing log from " + logPath);
			var fileBytes = System.IO.File.ReadAllBytes(logPath);
			var fileName = Path.GetFileName(logPath);
			return File(fileBytes, MediaTypeNames.Application.Octet, fileName);
		}

		[HttpPost("DbUpload")]
		public async Task<IActionResult> ImportDb(List<IFormFile> files) {
			var size = files.Sum(f => f.Length);

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

			// process uploaded files
			// Don't rely on or trust the FileName property without validation.
			return Ok(new {count = files.Count, size, filePaths, imported});
		}

		// POST: api/DreamData/mode
		[HttpPost("mode")]
		public async Task<IActionResult> DevMode([FromBody] int mode) {
			Log.Debug("Mode set to: " + mode);
			await _controlService.SetMode(mode);
			return Ok(mode);
		}

		// POST: api/DreamData/refreshDevices
		[HttpPost("scanDevices")]
		public async Task<IActionResult> ScanDevices() {
			await _controlService.ScanDevices();
			Thread.Sleep(5000);
			return new JsonResult(DataUtil.GetStoreSerialized());
		}

		[HttpPost("loadData")]
		public IActionResult LoadData([FromBody] JObject foo) {
			return new JsonResult(DataUtil.GetStoreSerialized());
		}

		// POST: api/DreamData/dsConnect
		[HttpPost("ambientShow")]
		public IActionResult PostShow([FromBody] JObject myDevice) {
			Log.Debug(@"Did it work (ambient Show)? " + JsonConvert.SerializeObject(myDevice));
			return Ok(myDevice);
		}

		[HttpGet("authorizeDevice")]
		public async Task<IActionResult> AuthorizeDevice([FromQuery] string id) {
			await _controlService.AuthorizeDevice(id);
			return Ok(id);
		}

		[HttpGet("getStats")]
		public IActionResult GetStats() {
			return Ok(CpuUtil.GetStats());
		}


		// POST: api/DreamData/ledData
		[HttpPost("systemData")]
		public async Task<IActionResult> UpdateSystem([FromBody] SystemData ld) {
			await _controlService.UpdateSystem(ld);
			return Ok(ld);
		}

		// POST: api/DreamData/ledData
		[HttpPost("startStream")]
		public async Task<IActionResult> StartStream([FromBody] GlimmrData gd) {
			await _controlService.StartStream(gd);
			return Ok(gd);
		}

		[HttpPost("systemControl")]
		public IActionResult SysControl([FromBody] string action) {
			ControlService.SystemControl(action);
			return Ok(action);
		}

		// POST: api/DreamData/updateDevice
		[HttpPost("updateDevice")]
		public async Task<IActionResult> UpdateDevice([FromBody] string dData) {
			var dObj = JObject.Parse(dData);
			Log.Debug("Update device fired: " + JsonConvert.SerializeObject(dObj));
			await _controlService.UpdateDevice(dObj, false);
			return Ok(dObj);
		}

		// POST: api/DreamData/flashDevice
		[HttpPost("flashDevice")]
		public async Task<IActionResult> FlashDevice([FromBody] string deviceId) {
			await _controlService.FlashDevice(deviceId);
			return Ok(deviceId);
		}

		// POST: api/DreamData/flashSector
		[HttpPost("flashSector")]
		public async Task<IActionResult> FlashSector([FromBody] int sector) {
			await _controlService.FlashSector(sector);
			return Ok(sector);
		}


		[HttpPost("flashLed")]
		public async Task<IActionResult> TestStripOffset([FromBody] int len) {
			Log.Debug("Get got: " + len);
			await _controlService.TestLights(len);
			return Ok(len);
		}


		[HttpPost("systemControl")]
		public IActionResult SystemControl(string action) {
			Log.Debug("Action triggered: " + action);
			switch (action) {
				case "restart":
					SystemUtil.Restart();
					break;
				case "shutdown":
					SystemUtil.Shutdown();
					break;
				case "reboot":
					SystemUtil.Reboot();
					break;
				case "update":
					SystemUtil.Update();
					break;
			}

			return Ok(action);
		}
	}
}