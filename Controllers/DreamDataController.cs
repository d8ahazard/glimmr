using HueDream.DreamScreen.Devices;
using HueDream.Hue;
using HueDream.HueDream;
using JsonFlatFileDataStore;
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
            DataStore store = DreamData.getStore();
            string message = "Unrecognized action";
            if (action == "getStatus") {
                DreamScreen.DreamClient ds = new DreamScreen.DreamClient(null);
                ds.getMode();
            }

            if (action == "connectDreamScreen") {
                string dsIp = store.GetItem("dsIp");
                DreamScreen.DreamClient ds = new DreamScreen.DreamClient(null);
                List<BaseDevice> dev = ds.findDevices().Result;
                Console.WriteLine("devices? " + JsonConvert.SerializeObject(dev));
                //store.ReplaceItemAsync("myDevices", dev.ToArray()); 
                return new JsonResult(dev);

            } else if (action == "authorizeHue") {
                if (!store.GetItem("hueAuth")) {
                    HueBridge hb = new HueBridge();
                    RegisterEntertainmentResult appKey = hb.checkAuth().Result;
                    Console.WriteLine("APPKEY: " + JsonConvert.SerializeObject(appKey));
                    if (appKey != null) {
                        Console.WriteLine("LINKED!");
                        message = "Success: Bridge Linked.";
                        store.ReplaceItemAsync("hueKey", appKey.StreamingClientKey);
                        store.ReplaceItemAsync("hueUser", appKey.Username);
                        store.ReplaceItemAsync("hueAuth", true);
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
                    store.ReplaceItemAsync("hueIp", bridgeIp);
                    message = "Success: Bridge IP is " + bridgeIp;
                } else {
                    message = "Error: No bridge found.";
                }
            }
            store.Dispose();
            return new JsonResult(message);
        }

        // GET: api/HueData/json
        [HttpGet("json")]
        public IActionResult Get() {
            Console.WriteLine("JSON GOT.");
            DataStore store = DreamData.getStore();
            if (store.GetItem("hueAuth")) {
                try {
                    HueBridge hb = new HueBridge();
                    store.ReplaceItem("hueLights", hb.getLights());
                    store.ReplaceItem("entertainmentGroups", hb.ListGroups().Result);
                } catch (AggregateException e) {
                    Console.WriteLine("An exception occurred fetching hue data: " + e);
                }
            }
            store.Dispose();
            return Ok(DreamData.GetStoreSerialized());
        }

        [HttpGet("lights")]
        public IActionResult GetWhatever() {
            return Ok(DreamData.getStore());
        }


        // GET: api/HueData/5
        [HttpGet("{id}", Name = "Get")]
        public static string Get(int id) {
            return "value";
        }

        // POST: api/HueData
        [HttpPost]
        public void Post() {
            DataStore store = DreamData.getStore();
            string devType = store.GetItem("emuType");
            BaseDevice myDevice;
            if (devType == "SideKick") {
                myDevice = store.GetItem<SideKick>("myDevice");
            } else {
                myDevice = store.GetItem<Connect>("myDevice");
            }
            string[] keys = Request.Form.Keys.ToArray();
            Console.WriteLine("We have a post: " + JsonConvert.SerializeObject(keys));
            bool mapLights = false;
            List<LightMap> lightMap = new List<LightMap>();
            foreach (string key in keys) {
                if (key.Contains("lightMap")) {
                    mapLights = true;
                    int lightId = int.Parse(key.Replace("lightMap", ""));
                    int sectorId = int.Parse(Request.Form[key]);
                    lightMap.Add(new LightMap(lightId, sectorId));
                } else if (key == "ds_type") {
                    if (myDevice.Tag != Request.Form[key]) {
                        if (Request.Form[key] == "Connect") {
                            myDevice = new Connect("localhost");
                        } else {
                            myDevice = new SideKick("localhost");
                        }
                        myDevice.Initialize();
                    }
                } else if (key == "dsGroup") {
                    Group[] groups = store.GetItem<Group[]>("entertainmentGroups");
                    foreach (Group g in groups) {
                        if (g.Id == Request.Form[key]) {
                            Console.WriteLine("Group match: " + JsonConvert.SerializeObject(g));
                            store.ReplaceItemAsync("entertainmentGroup", g);
                        }
                    }
                }
            }
            if (mapLights) {
                Console.WriteLine("Updating light map");
                store.ReplaceItemAsync("hueMap", lightMap);
            }
            store.Dispose();
        }
    }
}
