using HueDream.HueControl;
using HueDream.HueDream;
using Microsoft.AspNetCore.Mvc;
using Q42.HueApi.Models.Bridge;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace HueDream.Controllers {
    [Route("api/[controller]")]
    [ApiController]
    public class HueDataController : ControllerBase {
        // GET: api/HueData/action?action=...
        [HttpGet("action")]
        public IActionResult Get(string action) {
            string message = "Unrecognized action";
            DreamData userData = new DreamData();
            Console.Write("Now we're cooking: " + action);
            if (action == "connectDreamScreen") {
                Console.Write("Connecting dream screen?");
                string dsIp = userData.DS_IP;
                DreamScreenControl.DreamScreen ds = new DreamScreenControl.DreamScreen(dsIp);
                if (dsIp == "0.0.0.0") {
                    List<string> devices = ds.findDevices();
                    if (devices.Count() > 0) {
                        userData.DS_IP = devices[0].Split(":")[0];
                        Console.WriteLine("We have a DS IP: " + userData.DS_IP);
                        userData.saveData();
                        ds.dreamScreenIp = IPAddress.Parse(userData.DS_IP);
                        message = "Success: " + userData.DS_IP;
                    }
                }
            } else if (action == "authorizeHue") {
                Console.WriteLine("Auth request");
                if (userData.HUE_IP != "0.0.0.0") {
                    HueBridge hb = new HueBridge();
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
                }
                Console.Write("Authorize hue");
            } else if (action == "findHue") {
                Console.Write("Find hue");
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
                    HueBridge hb = new HueBridge();
                    userData.HUE_LIGHTS = hb.getLights();
                    userData.saveData();
                }
            }
            return Ok(message);
        }

        // GET: api/HueData/json
        [HttpGet("json")]
        public IActionResult Get() {
            Console.Write("Normal JSON request");
            DreamData userData = new DreamData();
            return Ok(userData);
        }

        [HttpGet("lights")]
        public IActionResult GetWhatever() {
            Console.Write("Light JSON request");
            DreamData userData = new DreamData();
            return Ok(userData);
        }


        // GET: api/HueData/5
        [HttpGet("{id}", Name = "Get")]
        public string Get(int id) {
            return "value";
        }

        // POST: api/HueData
        [HttpPost]
        public void Post(string value) {
            DreamData userData = new DreamData();
            string[] keys = Request.Form.Keys.ToArray<string>();
            Console.WriteLine("We have a post: " + value);
            List<KeyValuePair<int, string>> lightMap = new List<KeyValuePair<int, string>>();
            foreach (string key in keys) {
                Console.WriteLine("We have a key and value: " + key + " " + Request.Form[key]);
                if (key.Contains("lightMap")) {
                    int lightId = Int32.Parse(key.Replace("lightMap", ""));
                    lightMap.Add(new KeyValuePair<int, string>(lightId, Request.Form[key]));
                } else if (key == "ds_ip") {
                    userData.DS_IP = Request.Form[key];
                } else if (key == "hue_ip") {
                    userData.HUE_IP = Request.Form[key];
                } else if (key == "ds_sync") {
                    string val = Request.Form[key];
                    bool res = false;
                    if (val == "on") {
                        res = true;
                    } else {
                        res = false;
                    }
                    userData.HUE_SYNC = res;
                }
            }
            userData.HUE_MAP = lightMap;
            userData.saveData();
            CheckSync(userData);
        }

        private void CheckSync(DreamData userData) {
            DreamSync ds = new DreamSync();
            if (userData.DS_IP != "0.0.0.0" && userData.HUE_SYNC && !ds.doSync) {
                Console.WriteLine("Starting Dreamscreen sync!");
                Task.Run(() => ds.startSync());
            } else if (!userData.HUE_SYNC && ds.doSync) {
                Console.WriteLine("Stopping sync.");
                ds.StopSync();
            }
        }

        // PUT: api/HueData/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value) {
        }

        // DELETE: api/ApiWithActions/5
        [HttpDelete("{id}")]
        public void Delete(int id) {
        }
    }
}
