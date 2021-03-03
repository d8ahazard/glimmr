#region

using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models;
using Glimmr.Models.ColorTarget.Led;
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

		[HttpGet("")]
		public IActionResult DefaultAction() {
			var sd = DataUtil.GetObject<SystemData>("SystemData");
			return new JsonResult(sd);
		}
		
		[HttpGet("brightness")]
		public async Task<IActionResult> SetBrightness([FromQuery] int value) {
			Log.Debug("Setting brightness: " + value);
			SystemData sd = DataUtil.GetObject<SystemData>("SystemData");
			sd.Brightness = value;
			await _controlService.UpdateSystem(sd);
			return Ok(value);
		}
		
		[HttpGet("toggleMode")]
		public async Task<IActionResult> ToggleMode() {
			SystemData sd = DataUtil.GetObject<SystemData>("SystemData");
			var prev = sd.PreviousMode;
			var mode = sd.DeviceMode;
			if (mode == 0) {
				await _controlService.SetMode(prev);	
			} else {
				await _controlService.SetMode(0);
			}
			
			return Ok();
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