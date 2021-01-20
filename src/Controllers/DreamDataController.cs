#region

using System.Collections.Generic;
using System.Threading;
using Glimmr.Models;
using Glimmr.Models.LED;
using Glimmr.Models.Util;
using Glimmr.Services;
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

		// POST: api/DreamData/mode
		[HttpPost("mode")]
		public IActionResult DevMode([FromBody] int mode) {
			Log.Debug("Mode set to: " + mode);
			_controlService.SetMode(mode);
			return Ok(mode);
		}
		
		// POST: api/DreamData/refreshDevices
		[HttpPost("scanDevices")]
		public IActionResult ScanDevices() {
			_controlService.ScanDevices();
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

		[HttpGet("authorizeHue")]
		public IActionResult AuthorizeHue([FromQuery] string id) {
			_controlService.AuthorizeHue(id);
			return Ok(id);
		}
		
		[HttpGet("authorizeNano")]
		public IActionResult AuthorizeNano([FromQuery] string id) {
			_controlService.AuthorizeNano(id);
			return Ok(id);
		}

		[HttpGet("getStats")]

		public IActionResult GetStats() {
			return Ok(CpuUtil.GetStats());
		}
		
		// POST: api/DreamData/ledData
		[HttpPost("ledData")]
		public IActionResult UpdateLed([FromBody] LedData ld) {
			_controlService.UpdateLed(ld);
			return Ok(ld);
		}
		
		// POST: api/DreamData/ledData
		[HttpPost("systemData")]
		public IActionResult UpdateSystem([FromBody] SystemData ld) {
			_controlService.UpdateSystem(ld);
			return Ok(ld);
		}

		[HttpPost("systemControl")]
		public IActionResult SysControl([FromBody] string action) {
			_controlService.SystemControl(action);
			return Ok(action);
		}
		
		// POST: api/DreamData/updateDevice
		[HttpPost("updateDevice")]
		public IActionResult UpdateDevice([FromBody] string dData) {
			var dObj = JObject.Parse(dData);
			Log.Debug("Update device fired: " + JsonConvert.SerializeObject(dObj));
			_controlService.UpdateDevice(dObj);
			return Ok(dObj);
		}
		
		// POST: api/DreamData/flashDevice
		[HttpPost("flashDevice")]
		public IActionResult FlashDevice([FromBody] string deviceId) {
			_controlService.FlashDevice(deviceId);
			return Ok(deviceId);
		}
		
		// POST: api/DreamData/flashSector
		[HttpPost("flashSector")]
		public IActionResult FlashSector([FromBody] int sector) {
			_controlService.FlashSector(sector);
			return Ok(sector);
		}
		
		
		[HttpPost("flashLed")]
		public IActionResult TestStripOffset([FromBody] int len) {
			Log.Debug("Get got: " + len);
			_controlService.TestLights(len);
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