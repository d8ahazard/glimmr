using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using HueDream.Models;
using HueDream.Models.DreamScreen;
using HueDream.Models.DreamScreen.Devices;
using HueDream.Models.Hue;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HueDream.Controllers {
    [Route("api/[controller]"), ApiController]
    public class DreamDataController : ControllerBase {
        // GET: api/DreamData/getMode
        [Route("getMode")]
        public static int GetMode() {
            var dev = DreamData.GetDeviceData();
            return dev.Mode;
        }


        // POST: api/HueData/mode
        [HttpPost("mode")]
        public IActionResult Mode([FromBody] int mode) {
            SetMode(mode);
            return Ok(mode);
        }

        // POST: api/HueData/bridges
        [HttpPost("bridges")]
        public IActionResult PostBridges([FromBody] List<JObject> bData) {
            Console.WriteLine(@"Bridge Post received..." + JsonConvert.SerializeObject(bData));
            var bridgeData = bData.Select(BridgeData.DeserializeBridgeData).ToList();
            DreamData.SetItem("bridges", bridgeData);
            return Ok(bData.Count);
        }

        // POST: api/HueData/dsIp
        [HttpPost("dsIp")]
        public IActionResult PostIp([FromBody] string dsIp) {
            Console.WriteLine(@"Did it work? " + dsIp);
            DreamData.SetItem("dsIp", dsIp);
            return Ok(dsIp);
        }

        // POST: api/HueData/dsSidekick
        [HttpPost("dsSidekick")]
        public IActionResult PostSk([FromBody] SideKick skDevice) {
            Console.WriteLine(@"Did it work? " + JsonConvert.SerializeObject(skDevice));
            DreamData.SetItem("myDevice", skDevice);
            return Ok("ok");
        }

        // POST: api/HueData/dsConnect
        [HttpPost("dsConnect")]
        public IActionResult PostDevice([FromBody] Connect myDevice) {
            Console.WriteLine(@"Did it work? " + JsonConvert.SerializeObject(myDevice));
            DreamData.SetItem("myDevice", myDevice);
            return Ok(myDevice);
        }

        [HttpGet("action")]
        public IActionResult Action(string action, string value = "") {
            var message = "Unrecognized action";
            var store = DreamData.GetStore();
            Console.WriteLine($@"{action} fired.");

            if (action == "connectDreamScreen") {
                List<BaseDevice> dev;
                using (var ds = new DreamClient()) {
                    dev = ds.FindDevices().Result;
                }

                store.Dispose();
                return new JsonResult(dev);
            }

            if (action == "authorizeHue") {
                var doAuth = true;
                var bridges = store.GetItem<List<BridgeData>>("bridges");
                BridgeData bd = null;
                var bridgeInt = -1;

                if (!string.IsNullOrEmpty(value)) {
                    var bCount = 0;
                    foreach (var b in bridges) {
                        if (b.Ip == value) {
                            bd = b;
                            bridgeInt = bCount;
                            doAuth = b.Key == null || b.User == null;
                        }

                        bCount++;
                    }
                }

                if (doAuth) {
                    var appKey = HueBridge.CheckAuth(value).Result;
                    if (appKey != null && bd != null) {
                        message = "Success: Bridge Linked.";
                        bd.Key = appKey.StreamingClientKey;
                        bd.User = appKey.Username;
                        bridges[bridgeInt] = bd;
                        store.ReplaceItem("bridges", bridges, true);
                    }
                    else {
                        message = "Error: Press the link button";
                    }
                }
                else {
                    message = "Success: Bridge Already Linked.";
                }
            }
            else if (action == "findHue") {
                var bridges = HueBridge.FindBridges(3);
                if (bridges != null)
                    store.ReplaceItem("bridges", bridges, true);
                else
                    message = "Error: No bridge found.";
            }

            Console.WriteLine(message);
            store.Dispose();
            return new JsonResult(message);
        }

        // GET: api/HueData/json
        [HttpGet("json")]
        public IActionResult GetJson() {
            Console.WriteLine(@"GetJson Called.");
            var store = DreamData.GetStore();
            if (DreamData.GetItem<List<BridgeData>>("bridges") != null) {
                var bridges = store.GetItem<List<BridgeData>>("bridges");
                var newBridges = HueBridge.FindBridges();
                var nb = new List<BridgeData>();
                var update = false;
                if (bridges.Count > 0)
                    foreach (var b in bridges) {
                        if (b.Key != null && b.User != null) {
                            var hb = new HueBridge(b);
                            b.SetLights(hb.GetLights());
                            b.SetGroups(hb.ListGroups().Result);
                            update = true;
                        }

                        nb.Add(b);
                    }

                foreach (var bb in newBridges) {
                    var exists = false;
                    foreach (var b in bridges)
                        if (bb.BridgeId == b.Id)
                            exists = true;

                    if (!exists) {
                        Console.WriteLine($@"Adding new bridge at {bb.IpAddress}.");
                        nb.Add(new BridgeData(bb.IpAddress, bb.BridgeId));
                        update = true;
                    }
                }


                if (update) {
                    bridges = nb;
                    store.ReplaceItem("bridges", bridges, true);
                }
            }


            if (store.GetItem("dsIp") == "0.0.0.0") {
                var dc = new DreamClient();
                dc.FindDevices().ConfigureAwait(true);
                dc.Dispose();
            }

            store.Dispose();
            return Content(DreamData.GetStoreSerialized(), "application/json");
        }


        private static void SetMode(int mode) {
            var dev = DreamData.GetDeviceData();
            DreamSender.SendUdpWrite(0x03, 0x01, new[] {(byte) mode}, 0x21, (byte) dev.GroupNumber,
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8888));
        }
    }
}