using HueDream.DreamScreen;
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
using System.Net;

namespace HueDream.Controllers {
    [Route("api/[controller]")]
    [ApiController]
    public class DreamDataController : ControllerBase {

        // GET: api/DreamData/mode
        [HttpGet("mode")]
        public int GetMode() {
            BaseDevice dev = DreamData.GetDeviceData();
            return dev.Mode;
        }

        [HttpPost]

        [Route("mode")]
        public IActionResult mode([FromBody] int mode) {
            SetMode(mode);
            return Ok(mode);
        }

        [HttpGet("action")]
        public JsonResult Get(string action) {
            DataStore store = DreamData.getStore();
            string message = "Unrecognized action";

            if (action == "connectDreamScreen") {
                Console.WriteLine("ConnectDreamScreen fired.");
                List<BaseDevice> dev = new List<BaseDevice>();
                using (DreamClient ds = new DreamClient()) {
                    dev = ds.FindDevices().Result;
                }
                return new JsonResult(dev);

            } else if (action == "authorizeHue") {
                if (!store.GetItem("hueAuth")) {
                    HueBridge hb = new HueBridge();
                    RegisterEntertainmentResult appKey = hb.checkAuth().Result;
                    if (appKey != null) {
                        Console.WriteLine("Bridge linked.");
                        message = "Success: Bridge Linked.";
                        store.ReplaceItemAsync("hueKey", appKey.StreamingClientKey);
                        store.ReplaceItemAsync("hueUser", appKey.Username);
                        store.ReplaceItemAsync("hueAuth", true);
                    } else {
                        Console.WriteLine("Bridge Link Failure.");
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
            if (store.GetItem("dsIp") == "0.0.0.0") {
                DreamClient dc = new DreamClient();
                dc.FindDevices();
            }
            store.Dispose();
            return Ok(DreamData.GetStoreSerialized());
        }

        [HttpGet("lights")]
        public IActionResult GetWhatever() {
            return Ok(DreamData.getStore());
        }

                
        // POST: api/HueData
        [HttpPost]
        public void Post() {
            BaseDevice myDevice = DreamData.GetDeviceData();
            DataStore store = DreamData.getStore();
            List<LightMap> map = store.GetItem<List<LightMap>>("hueMap");            
            Group[] groups = store.GetItem<Group[]>("entertainmentGroups");
            store.Dispose();
            Group entGroup = DreamData.GetItem<Group>("entertainmentGroup");
            string curId = (entGroup == null) ? "-1" : entGroup.Id;           
            string[] keys = Request.Form.Keys.ToArray();
            Console.WriteLine("We have a post: " + JsonConvert.SerializeObject(Request.Form));
            bool mapLights = false;
            List<LightMap> lightMap = new List<LightMap>();
            int curMode = myDevice.Mode;
            foreach (string key in keys) {
                if (key.Contains("lightMap")) {
                    mapLights = true;
                    int lightId = int.Parse(key.Replace("lightMap", ""));
                    int sectorId = int.Parse(Request.Form[key]);
                    bool overrideB = (Request.Form["overrideBrightness" + lightId] == "on");
                    int newB = int.Parse(Request.Form["brightness" + lightId]);
                    lightMap.Add(new LightMap(lightId, sectorId, overrideB, newB));
                } else if (key == "ds_type") {
                    if (myDevice.Tag != Request.Form[key]) {
                        if (Request.Form[key] == "Connect") {
                            myDevice = new Connect("localhost");
                        } else {
                            myDevice = new SideKick("localhost");
                        }
                        myDevice.Initialize();
                        DreamData.SetItem("myDevice", myDevice);
                        DreamData.SetItem<string>("emuType", Request.Form[key]);
                    }
                } else if (key == "dsGroup" && curId != Request.Form[key]) {
                    foreach (Group g in groups) {
                        if (g.Id == Request.Form[key]) {
                            Console.WriteLine("Group match: " + JsonConvert.SerializeObject(g));
                            if (curMode != 0) {
                                SetMode(0);
                            }

                            DreamData.SetItem("entertainmentGroup", g);

                            if (curMode != 0) {
                                SetMode(curMode);
                            }
                        }
                    }
                }
            }

            if (mapLights) {
                // Check to see if the map actually changed
                if (JsonConvert.SerializeObject(map) != JsonConvert.SerializeObject(lightMap)) {
                    Console.WriteLine("Updating light map: " + JsonConvert.SerializeObject(lightMap));
                    if (curMode != 0) {
                        SetMode(0);
                    }
                    // Now update data, and wait
                    DreamData.SetItem("hueMap", lightMap);
                    // Now restart with new mappings
                    if (curMode != 0) {
                        SetMode(curMode);
                    }
                }
            }
        }

        private void SetMode(int mode) {
            DreamSender.SendUDPWrite(0x03, 0x01, new byte[] { (byte)mode }, 0x21, 0, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8888));
        }

    }
}
