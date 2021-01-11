#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using Glimmr.Hubs;
using Glimmr.Models;
using Glimmr.Models.LED;
using Glimmr.Models.StreamingDevice.Hue;
using Glimmr.Models.StreamingDevice.LIFX;
using Glimmr.Models.StreamingDevice.Nanoleaf;
using Glimmr.Models.Util;
using Glimmr.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

#endregion

namespace Glimmr.Controllers {
	[Route("api/[controller]")]
	[ApiController]
	public class DreamDataController : ControllerBase {
		private readonly IHubContext<SocketServer> _hubContext;
		private readonly ControlService _controlService;

		public DreamDataController(IHubContext<SocketServer> hubContext, ControlService controlService) {
			_hubContext = hubContext;
			_controlService = controlService;
		}

		// POST: api/DreamData/mode
		[HttpPost("mode")]
		public IActionResult DevMode([FromBody] int mode) {
			_controlService.SetMode(mode);
			_controlService.NotifyClients();
			return Ok(mode);
		}

		// GET: LED TEST
		[HttpGet("corners")]
		public IActionResult TestStrip([FromQuery] int len, bool stop = false) {
			Log.Debug("Get got: " + len);
			_controlService.TestLeds(len, stop, 0);
			return Ok(len);
		}

		[HttpGet("offset")]
		public IActionResult TestStripOffset([FromQuery] int len, bool stop = false) {
			Log.Debug("Get got: " + len);
			_controlService.TestLeds(len, stop, 1);
			return Ok(len);
		}

		[HttpPost("updateDs")]
		public IActionResult UpdateDs([FromBody] JObject dsSetting) {
			if (dsSetting == null) throw new ArgumentException("Invalid Jobject.");
			var id = (dsSetting["Id"] ?? "").Value<string>();
			var property = (dsSetting["Property"] ?? "").Value<string>();
			var value = (dsSetting["Value"] ?? "").Value<string>();
			Log.Debug($"We got our stuff: {id}, {property}, {value}");
			_controlService.SendDreamMessage(property, value, id);
			ControlUtil.NotifyClients(_hubContext);
			return Ok();
		}

		// POST: api/DreamData/updateDevice
		[HttpPost("updateDevice")]
		public IActionResult UpdateDevice([FromBody] JObject dData) {
			var res = ControlUtil.TriggerReload(_hubContext, dData).Result;
			_controlService.RefreshDevice( (string) dData.GetValue("_id"));
			ControlUtil.NotifyClients(_hubContext);
			return Ok(res);
		}

		// POST: api/DreamData/updateDevice
		[HttpPost("updateData")]
		public IActionResult UpdateData([FromBody] JObject dData) {
			var res = ControlUtil.TriggerReload(_hubContext, dData).Result;
			ControlUtil.NotifyClients(_hubContext);
			return Ok(res);
		}

		// POST: api/DreamData/capturemode
		[HttpPost("capturemode")]
		public IActionResult CaptureMode([FromBody] int cMode) {
			ControlUtil.SetCaptureMode(_hubContext, cMode);
			ControlUtil.NotifyClients(_hubContext);
			return Ok(cMode);
		}

		// POST: api/DreamData/camType
		[HttpPost("camType")]
		public IActionResult CamType([FromBody] int cType) {
			Log.Debug("Camera type set to " + cType);
			DataUtil.SetItem<int>("CamType", cType);
			ControlUtil.NotifyClients(_hubContext);
			_controlService.ResetMode();
			return Ok(cType);
		}

		// POST: api/DreamData/ledData
		[HttpPost("updateLed")]
		public IActionResult UpdateLed([FromBody] LedData ld) {
			Log.Debug("Got LD from post: " + JsonConvert.SerializeObject(ld));
			DataUtil.SetObject<LedData>("LedData", ld);
			_controlService.RefreshLedData();
			_controlService.NotifyClients();
			return Ok(ld);
		}

		// POST: api/DreamData/vcount
		[HttpPost("vcount")]
		public IActionResult Vcount([FromBody] int count) {
			var ledData = DataUtil.GetCollection<LedData>("ledData").First();
			var capMode = DataUtil.GetItem<int>("CaptureMode");
			int hCount;
			if (capMode == 0) {
				hCount = ledData.HCountDs;
				ledData.VCountDs = count;
			} else {
				hCount = ledData.TopCount;
				ledData.LeftCount = count;
			}

			ledData.LedCount = hCount * 2 + count * 2;
			DataUtil.SetObject<LedData>("LedData", ledData);
			_controlService.NotifyClients();
			_controlService.ResetMode();
			return Ok(count);
		}

		// POST: api/DreamData/hcount
		[HttpPost("hcount")]
		public IActionResult Hcount([FromBody] int count) {
			var ledData = DataUtil.GetCollection<LedData>("ledData").First();
			var capMode = DataUtil.GetItem<int>("CaptureMode");
			int vCount;
			if (capMode == 0) {
				vCount = ledData.VCountDs;
				ledData.HCountDs = count;
			} else {
				vCount = ledData.LeftCount;
				ledData.TopCount = count;
			}

			ledData.LedCount = vCount * 2 + count * 2;
			DataUtil.SetObject<LedData>("LedData", ledData);
			_controlService.NotifyClients();
			_controlService.ResetMode();
			return Ok(count);
		}

		// POST: api/DreamData/stripType
		[HttpPost("stripType")]
		public IActionResult StripType([FromBody] int type) {
			var ledData = DataUtil.GetCollection<LedData>("ledData").First();
			ledData.StripType = type;
			Log.Debug("Updating LED Data: " + JsonConvert.SerializeObject(ledData));
			DataUtil.SetObject<LedData>("LedData", ledData);
			_controlService.NotifyClients();
			_controlService.ResetMode();
			return Ok(type);
		}


		// POST: api/DreamData/dsIp
		[HttpPost("dsIp")]
		public IActionResult PostIp([FromBody] string dsIp) {
			Log.Debug(@"Did it work? " + dsIp);
			DataUtil.SetItem("DsIp", dsIp);
			_controlService.NotifyClients();
			_controlService.ResetMode();
			return Ok(dsIp);
		}

		// POST: api/DreamData/dsConnect
		[HttpPost("ambientColor")]
		public IActionResult PostColor([FromBody] JObject myDevice) {
			if (myDevice == null) throw new ArgumentException("Invalid argument.");
			Log.Debug(@"Did it work (ambient Color)? " + JsonConvert.SerializeObject(myDevice));
			var id = (string) myDevice.GetValue("device", StringComparison.InvariantCulture);
			var value = myDevice.GetValue("color", StringComparison.InvariantCulture);
			var group = (int) myDevice.GetValue("group", StringComparison.InvariantCulture);
			var color = ColorTranslator.FromHtml("#" + value);
			// If setting this from a group, set it for all devices in that group
			if (int.TryParse(id, out var idNum)) {
				var devs = DataUtil.GetDreamDevices();
				foreach (var dev in devs.Where(dev => dev.DeviceGroup == idNum))
					_controlService.SetAmbientColor(color, dev.IpAddress, idNum);
			} else {
				// Otherwise, just target the specified device.
				_controlService.SetAmbientColor(color, id, group);
			}

			//ControlUtil.NotifyClients(_hubContext);
			return Ok("Ok");
		}

		// POST: api/DreamData/dsConnect
		[HttpPost("ambientMode")]
		public IActionResult PostAmbientMode([FromBody] JObject myDevice) {
			Log.Debug(@"Did it work (ambient Mode)? " + JsonConvert.SerializeObject(myDevice));
			_controlService.SendDreamMessage("ambientModeType", (int) myDevice.GetValue("mode"),
				(string) myDevice.GetValue("id"));

			//ControlUtil.NotifyClients(_hubContext);
			return Ok("Ok");
		}

		// POST: api/DreamData/refreshDevices
		public IActionResult RefreshDevices() {
			_controlService.RescanDevices();
			return new JsonResult("Refreshing...");
		}

		// POST: api/DreamData/dsConnect
		[HttpPost("ambientShow")]
		public IActionResult PostShow([FromBody] JObject myDevice) {
			Log.Debug(@"Did it work (ambient Show)? " + JsonConvert.SerializeObject(myDevice));
			_controlService.SendDreamMessage("ambientScene", myDevice.GetValue("scene"),
				(string) myDevice.GetValue("id"));
			//ControlUtil.NotifyClients(_hubContext);
			return Ok(myDevice);
		}
		
		[HttpPost("systemData")]
		public IActionResult SystemData(SystemData sd) {
			Log.Debug("Updating system data.");
			DataUtil.SetObject<SystemData>("SystemData", sd);
			return Ok(sd);
		}
		
		[HttpPost("ledData")]
		public IActionResult LedData(LedData ld) {
			Log.Debug("Updating LED Data.");
			DataUtil.SetObject<LedData>("LedData", ld);
			return Ok(ld);
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


		[HttpGet("action")]
		public IActionResult Action(string action, string value = "") {
			var message = "Unrecognized action";
			//Log.Debug($@"{action} called from Web API.");
			switch (action) {
				case "loadData":
					_controlService.NotifyClients();
					return Content(DataUtil.GetStoreSerialized(), "application/json");
				case "refreshDevices":
					Log.Debug("Triggering refresh?");
					// Just trigger dreamclient to refresh devices
					_controlService.RescanDevices();
					return new JsonResult("OK");
				case "authorizeHue": {
					Log.Debug("AuthHue called, for reaal: " + value);
					var doAuth = true;
					HueData bd = null;
					if (!string.IsNullOrEmpty(value)) {
						_hubContext?.Clients.All.SendAsync("hueAuth", "start");
						bd = DataUtil.GetCollectionItem<HueData>("Dev_Hue", value);
						Log.Debug("BD: " + JsonConvert.SerializeObject(bd));
						if (bd == null) {
							Log.Debug("Null bridge retrieved.");
							return new JsonResult(null);
						}

						if (bd.Key != null && bd.User != null) {
							Log.Debug("Bridge is already authorized.");
							doAuth = false;
						}
					} else {
						Log.Warning("Null value.");
						doAuth = false;
					}

					if (!doAuth) {
						Log.Debug("No auth, returning existing data.");
						return new JsonResult(bd);
					}

					Log.Debug("Trying to retrieve appkey...");
					var appKey = HueDiscovery.CheckAuth(bd.IpAddress).Result;
					if (appKey == null) {
						Log.Debug("Error retrieving app key.");
						return new JsonResult(bd);
					}

					bd.Key = appKey.StreamingClientKey;
					bd.User = appKey.Username;
					Log.Debug("We should be authorized, returning.");
					DataUtil.InsertCollection<HueData>("Dev_Hue", bd);
					return new JsonResult(bd);
				}
				case "authorizeNano": {
					Log.Debug("Nano auth triggered for " + value);
					var doAuth = false;
					NanoleafData dev = null;
					if (!string.IsNullOrEmpty(value)) {
						dev = DataUtil.GetCollectionItem<NanoleafData>("Dev_Nanoleaf", value);
						if (dev != null) {
							Log.Debug("Dev found.");
							if (dev.Token == null) {
								Log.Debug("Trying auth, no token.");
								doAuth = true;
							}
						}
					}

					if (doAuth) {
						var panel = new NanoleafDevice(dev, _controlService.HttpSender);
						var appKey = panel.CheckAuth().Result;
						if (appKey != null) {
							Log.Debug("Retrieved app key, saving.");
							dev.Token = appKey.Token;
							dev.RefreshLeaf();
							DataUtil.InsertCollection<NanoleafData>("Dev_Nanoleaf", dev);
							Log.Debug("Leaf refreshed and set...");
						} else {
							Log.Debug("No appkey...");
						}
						panel.Dispose();
					}

					Log.Debug("Returning.");
					return new JsonResult(dev);
				}
			}

			Log.Debug(message);
			return new JsonResult(message);
		}

		// GET: api/DreamData/json
		[HttpGet("json")]
		public IActionResult GetJson() {
			Log.Debug(@"GetJson Called.");
			var bridgeArray = DataUtil.GetCollection<HueData>("Dev_Hue");
			if (bridgeArray.Count == 0) {
				var newBridges = HueDiscovery.Discover().Result;
				foreach (var b in newBridges) DataUtil.InsertCollection<HueData>("Dev_Hue", b);
			}

			return Content(DataUtil.GetStoreSerialized(), "application/json");
		}

		private void SetBrightness(JObject dData) {
			if (dData == null) throw new ArgumentException("invalid jobject");
			var tag = (dData["tag"] ?? "INVALID").Value<string>();
			var id = (dData["id"] ?? "INVALID").Value<string>();
			var brightness = (dData["brightness"] ?? -1).Value<int>();
			Log.Debug($"Setting brightness for {tag} {id} to {brightness}.");
			var myDev = DataUtil.GetDeviceData();
			var ipAddress = myDev.IpAddress;
			var groupNumber = (byte) myDev.DeviceGroup;
			var sendRemote = false;
			var remoteId = "";
			switch (tag) {
				case "Hue":
					var bridge = DataUtil.GetCollectionItem<HueData>("Dev_Hue", id);
					bridge.MaxBrightness = brightness;
					DataUtil.InsertCollection<HueData>("Dev_Hue", bridge);
					sendRemote = true;
					remoteId = bridge.Id;
					break;
				case "Lifx":
					var bulb = DataUtil.GetCollectionItem<LifxData>("Dev_Lifx", id);
					bulb.MaxBrightness = brightness;
					DataUtil.InsertCollection<LifxData>("Dev_Lifx", bulb);
					sendRemote = true;
					remoteId = bulb.Id;
					break;
				case "Nanoleaf":
					var panel = DataUtil.GetCollectionItem<NanoleafData>("Dev_Nanoleaf", id);
					panel.MaxBrightness = brightness;
					DataUtil.InsertCollection<NanoleafData>("Dev_Nanoleaf", panel);
					sendRemote = true;
					remoteId = panel.Id;
					break;
				default:
					var groupSend = false;
					byte mFlag = 0x11;
					if (ipAddress == "255.255.255.0")
						groupSend = true;
					else
						mFlag = 0x21;

					_controlService.SendUdpWrite(0x03, 0x02, new[] {ByteUtils.IntByte(brightness)}, mFlag, groupNumber,
						new IPEndPoint(IPAddress.Parse(ipAddress), 8888), groupSend);
					break;
			}

			if (!sendRemote) return;
			var payload = new List<byte> {ByteUtils.IntByte(brightness)};
			var utf8 = new UTF8Encoding();
			payload.AddRange(utf8.GetBytes(remoteId));
			_controlService.SendUdpWrite(0x01, 0x10, payload.ToArray(), 0x21, groupNumber,
				new IPEndPoint(IPAddress.Parse(ipAddress), 8888));
		}
	}
}