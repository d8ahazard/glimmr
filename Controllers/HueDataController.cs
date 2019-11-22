using HueDream.HueControl;
using HueDream.HueDream;
using Microsoft.AspNetCore.Mvc;
using Q42.HueApi.Models.Bridge;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HueDream.Controllers {
    [Route("api/[controller]")]
    [ApiController]
    public class HueDataController : ControllerBase {
        private DreamData userData;
        private static DreamSync ds;
        public HueDataController() {
            userData = new DreamData();
            ds = new DreamSync();
        }

        // GET: api/HueData/action?action=...
        [HttpGet("action")]
        public JsonResult Get(string action) {
            string message = "Unrecognized action";
            Console.Write("Now we're cooking: " + action);
            if (action == "getStatus") {
                DreamScreenControl.DreamScreen ds = new DreamScreenControl.DreamScreen(null, userData);
                ds.getMode();
            }

            if (action == "connectDreamScreen") {
                string dsIp = userData.DS_IP;
                DreamScreenControl.DreamScreen ds = new DreamScreenControl.DreamScreen(null, userData);
                if (dsIp == "0.0.0.0") {
                    ds.findDevices();
                } else {
                    Console.WriteLine("Searching for devices for the fun of it.");
                    ds.findDevices();
                }
            } else if (action == "authorizeHue") {
                if (userData.HUE_IP != "0.0.0.0") {
                    HueBridge hb = new HueBridge(userData);
                    RegisterEntertainmentResult appKey = hb.checkAuth();
                    if (appKey == null) {
                        message = "Error: Press the link button";
                    } else {
                        message = "Success: Bridge Linked.";
                        userData.HUE_KEY = appKey.StreamingClientKey;
                        userData.HUE_USER = appKey.Username;
                        userData.HUE_AUTH = true;
                        userData.saveData();
                    }
                } else {
                    message = "No Operation: Bridge Already Linked.";
                }
            } else if (action == "findHue") {
                string bridgeIp = HueBridge.findBridge();
                if (bridgeIp != "") {
                    userData.HUE_IP = bridgeIp;
                    userData.saveData();
                    message = "Success: Bridge IP is " + bridgeIp;
                } else {
                    message = "Error: No bridge found.";
                }
            } else if (action == "getLights") {
                if (userData.HUE_AUTH) {
                    HueBridge hb = new HueBridge(userData);
                    userData.HUE_LIGHTS = hb.getLights();
                    userData.saveData();
                    return new JsonResult(userData.HUE_LIGHTS);
                } else {
                    message = "Error: Link your hue bridge first.";
                }
            }
            return new JsonResult(message);
        }

        // GET: api/HueData/json
        [HttpGet("json")]
        public IActionResult Get() {
            Console.WriteLine("JSON GOT.");
            return Ok(userData);
        }

        [HttpGet("lights")]
        public IActionResult GetWhatever() {
            return Ok(userData);
        }


        // GET: api/HueData/5
        [HttpGet("{id}", Name = "Get")]
        public static string Get(int id) {
            return "value";
        }

        // POST: api/HueData
        [HttpPost]
        public void Post(string value) {
            string[] keys = Request.Form.Keys.ToArray<string>();
            Console.WriteLine("We have a post: " + value);
            bool mapLights = false;
            bool sync = userData.HUE_SYNC;
            bool enableSync = false;
            List<KeyValuePair<int, string>> lightMap = new List<KeyValuePair<int, string>>();
            foreach (string key in keys) {
                Console.WriteLine("We have a key and value: " + key + " " + Request.Form[key]);
                if (key.Contains("lightMap")) {
                    mapLights = true;
                    int lightId = int.Parse(key.Replace("lightMap", ""));
                    lightMap.Add(new KeyValuePair<int, string>(lightId, Request.Form[key]));
                } else if (key == "ds_ip") {
                    userData.DS_IP = Request.Form[key];
                } else if (key == "hue_ip") {
                    userData.HUE_IP = Request.Form[key];
                } else if (key == "hue_sync") {
                    if ((string)Request.Form[key] == "true") {
                        Console.WriteLine("TRUE");
                        enableSync = true;
                    }

                    userData.HUE_SYNC = enableSync;
                }
            }
            if (mapLights) userData.HUE_MAP = lightMap;
            userData.saveData();
            ds.CheckSync(enableSync);

        }


    }
}
