using HueDream.DreamScreen.Devices;
using HueDream.Hue;
using HueDream.HueDream;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Q42.HueApi.Models.Bridge;
using Q42.HueApi.Models.Groups;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HueDream.Controllers {
    [Route("api/[controller]")]
    [ApiController]
    public class DreamDataController : ControllerBase {

        // GET: api/HueData/action?action=...
        [HttpGet("action")]
        public JsonResult Get(string action) {
            DataObj userData = DreamData.dataObj;
            string message = "Unrecognized action";
            if (action == "getStatus") {
                DreamScreen.DreamClient ds = new DreamScreen.DreamClient(null, userData);
                ds.getMode();
            }

            if (action == "connectDreamScreen") {
                string dsIp = userData.DsIp;
                DreamScreen.DreamClient ds = new DreamScreen.DreamClient(null, userData);
                if (dsIp == "0.0.0.0") {
                    ds.findDevices();
                } else {
                    Console.WriteLine("Searching for devices for the fun of it.");
                    List<BaseDevice> dev = ds.findDevices().Result;
                    Console.WriteLine("Devices? " + JsonConvert.SerializeObject(dev));
                    //userData.MyDevices = dev.ToArray();
                    DreamData.SaveJson(userData);
                    return new JsonResult(dev);
                }
            } else if (action == "authorizeHue") {
                if (!userData.HueAuth) {
                    HueBridge hb = new HueBridge(userData);
                    RegisterEntertainmentResult appKey = hb.checkAuth().Result;
                    Console.WriteLine("APPKEY: " + JsonConvert.SerializeObject(appKey));
                    if (appKey != null) {
                        Console.WriteLine("LINKED!");
                        message = "Success: Bridge Linked.";
                        userData.HueKey = appKey.StreamingClientKey;
                        userData.HueUser = appKey.Username;
                        userData.HueAuth = true;
                        DreamData.SaveJson(userData);
                    } else {
                        Console.WriteLine("NOT LINKED");
                        message = "Error: Press the link button";
                    }

                } else {
                    message = "Success: Bridge Already Linked.";
                }

            } else if (action == "findHue") {
                string bridgeIp = HueBridge.findBridge();
                if (string.IsNullOrEmpty(bridgeIp)) {
                    userData.HueIp = bridgeIp;
                    DreamData.SaveJson(userData);
                    message = "Success: Bridge IP is " + bridgeIp;
                } else {
                    message = "Error: No bridge found.";
                }
            }
            return new JsonResult(message);
        }

        // GET: api/HueData/json
        [HttpGet("json")]
        public IActionResult Get() {
            Console.WriteLine("JSON GOT.");
            DataObj doo = DreamData.dataObj;
            if (doo.HueAuth) {
                try {
                    HueBridge hb = new HueBridge(doo);
                    doo.HueLights = hb.getLights();
                    doo.EntertainmentGroups = hb.ListGroups().Result;
                    DreamData.SaveJson(doo);
                } catch (AggregateException e) {
                    Console.WriteLine("An exception occurred fetching hue data: " + e);
                }
            }
            return Ok(doo);
        }

        [HttpGet("lights")]
        public IActionResult GetWhatever() {
            return Ok(DreamData.dataObj);
        }


        // GET: api/HueData/5
        [HttpGet("{id}", Name = "Get")]
        public static string Get(int id) {
            return "value";
        }

        // POST: api/HueData
        [HttpPost]
        public void Post() {
            DataObj userData = DreamData.LoadJson();
            string[] keys = Request.Form.Keys.ToArray<string>();
            Console.WriteLine("We have a post: " + JsonConvert.SerializeObject(keys));
            bool mapLights = false;
            List<KeyValuePair<int, int>> lightMap = new List<KeyValuePair<int, int>>();
            foreach (string key in keys) {
                if (key.Contains("lightMap")) {
                    mapLights = true;
                    int lightId = int.Parse(key.Replace("lightMap", ""));
                    lightMap.Add(new KeyValuePair<int, int>(lightId, int.Parse(Request.Form[key])));
                } else if (key == "ds_type") {
                    if (userData.MyDevice.Tag != Request.Form[key]) {
                        if (Request.Form[key] == "Connect") {
                            userData.MyDevice = new Connect("localhost");
                        } else {
                            userData.MyDevice = new SideKick("localhost");
                        }
                        userData.MyDevice.Initialize();
                    }
                } else if (key == "dsGroup") {
                    Group[] groups = userData.EntertainmentGroups;
                    foreach (Group g in groups) {
                        if (g.Id == Request.Form[key]) {
                            Console.WriteLine("Group match: " + JsonConvert.SerializeObject(g));
                            userData.EntertainmentGroup = g;
                        }
                    }
                }
            }
            if (mapLights) {
                Console.WriteLine("Updating light map");
                userData.HueMap = lightMap;
            }

            DreamData.SaveJson(userData);
        }
    }
}
