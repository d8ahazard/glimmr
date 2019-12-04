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
        public static int GetMode() {
            var dev = DreamData.GetDeviceData();
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
            var message = "Unrecognized action";
            var store = DreamData.GetStore();
            Console.WriteLine($"{action} fired.");

            if (action == "connectDreamScreen") {
                var dev = new List<BaseDevice>();
                using (var ds = new DreamClient()) {
                    dev = ds.FindDevices().Result;
                }
                store.Dispose();
                return new JsonResult(dev);

            } else if (action == "authorizeHue") {
                if (!store.GetItem("hueAuth")) {
                    var hb = new HueBridge();
                    var appKey = hb.CheckAuth().Result;
                    if (appKey != null) {
                        message = "Success: Bridge Linked.";
                        store.ReplaceItemAsync("hueKey", appKey.StreamingClientKey);
                        store.ReplaceItemAsync("hueUser", appKey.Username);
                        store.ReplaceItemAsync("hueAuth", true);
                    } else {
                        message = "Error: Press the link button";
                    }

                } else {
                    message = "Success: Bridge Already Linked.";
                }

            } else if (action == "findHue") {
                var bridgeIp = HueBridge.FindBridge();
                if (string.IsNullOrEmpty(bridgeIp)) {
                    store.ReplaceItemAsync("hueIp", bridgeIp);
                    message = "Success: Bridge IP is " + bridgeIp;
                } else {
                    message = "Error: No bridge found.";
                }
            }
            Console.WriteLine(message);
            store.Dispose();
            return new JsonResult(message);
        }

        // GET: api/HueData/json
        [HttpGet("json")]
        public IActionResult Get() {
            var store = DreamData.GetStore();
            if (store.GetItem("hueAuth")) {
                try {
                    var hb = new HueBridge();
                    store.ReplaceItem("hueLights", hb.GetLights());
                    store.ReplaceItem("entertainmentGroups", hb.ListGroups().Result);
                } catch (AggregateException e) {
                    Console.WriteLine("An exception occurred fetching hue data: " + e);
                }
            }
            if (store.GetItem("dsIp") == "0.0.0.0") {
                var dc = new DreamClient();
                dc.FindDevices().ConfigureAwait(true);
                dc.Dispose();
            }
            store.Dispose();
            return Ok(DreamData.GetStoreSerialized());
        }



        // POST: api/HueData
        [HttpPost]
        public void Post() {
            var myDevice = DreamData.GetDeviceData();
            var store = DreamData.GetStore();
            var map = store.GetItem<List<LightMap>>("hueMap");
            var groups = store.GetItem<Group[]>("entertainmentGroups");
            store.Dispose();
            Group entGroup = DreamData.GetItem<Group>("entertainmentGroup");
            var curId = (entGroup == null) ? "-1" : entGroup.Id;
            var keys = Request.Form.Keys.ToArray();
            Console.WriteLine("We have a post: " + JsonConvert.SerializeObject(Request.Form));
            var mapLights = false;
            var lightMap = new List<LightMap>();
            var curMode = myDevice.Mode;
            foreach (var key in keys) {
                if (key.Contains("lightMap")) {
                    mapLights = true;
                    var lightId = int.Parse(key.Replace("lightMap", ""));
                    var sectorId = int.Parse(Request.Form[key]);
                    var overrideB = (Request.Form["overrideBrightness" + lightId] == "on");
                    var newB = int.Parse(Request.Form["brightness" + lightId]);
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
                    foreach (var g in groups) {
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

        private static void SetMode(int mode) {
            DreamSender.SendUdpWrite(0x03, 0x01, new byte[] { (byte)mode }, 0x21, 0, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8888));
        }

    }
}
