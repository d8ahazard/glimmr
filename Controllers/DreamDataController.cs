using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using HueDream.Models;
using HueDream.Models.DreamScreen;
using HueDream.Models.DreamScreen.Devices;
using HueDream.Models.Hue;
using HueDream.Models.Util;
using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using HueDream.Models.Nanoleaf;
using Nanoleaf.Client;

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

            if (action == "listDevices") {
                var bridges = DreamData.GetItem<JToken>("bridges");
                var leaves = DreamData.GetItem<JToken>("leaves");
                var jd = DreamData.GetItem<JToken>("devices");
                JObject devList = new JObject {
                    ["bridges"] = bridges,
                    ["leaves"] = leaves,
                    ["ds"] = jd
                };
                return new JsonResult(devList);
            }

            if (action == "findDreamDevices") {
                List<BaseDevice> dev;
                using (var ds = new DreamClient()) {
                    dev = ds.FindDevices().Result;
                }

                store.Dispose();
                return new JsonResult(dev);
            }

            if (action == "refreshNanoLeaf") {
                var leaves = DreamData.GetItem<List<NanoData>>("leaves");
                NanoData nd = null;
                var leafInt = 0;
                foreach( NanoData leaf in leaves) {
                    if (leaf.IpV4Address == value) {
                        nd = leaf;
                        break;
                    }
                    leafInt++;
                } 
                if (nd != null) {
                    var panel = new Panel(nd.IpV4Address, nd.Token);
                    var layout = panel.GetLayout().Result;
                    if (layout != null) {
                        nd.Layout = layout;
                        leaves[leafInt] = nd;
                        DreamData.SetItem<List<NanoData>>("leaves", leaves);
                    }
                    return new JsonResult(layout);
                }
                message = "You suck, supply an IP.";                
            }

            if (action == "findNanoLeaf") {
                LogUtil.Write("Find Nano Leaf called.");
                var existingLeaves = DreamData.GetItem<List<NanoData>>("leaves");
                var leaves = Discovery.Discover(2);
                var all = new List<NanoData>();
                LogUtil.Write("Got all devices: " + JsonConvert.SerializeObject(existingLeaves));
                if (existingLeaves != null) {
                    LogUtil.Write("Adding range.");
                    foreach(var newLeaf in leaves) {
                        var add = true;
                        var exint = 0;
                        foreach (var leaf in existingLeaves) {
                            if (leaf.Id == newLeaf.Id)
                            {
                                LogUtil.Write("Updating existing leaf.");
                                newLeaf.Token= leaf.Token;
                                existingLeaves[exint] = newLeaf;
                                add = false;
                                break;
                            }
                            exint++;
                        }
                        if (add) {
                            LogUtil.Write("Adding new leaf.");
                            all.Add(newLeaf);
                        }
                    }
                    all.AddRange(existingLeaves);
                }
                else
                {
                    all.AddRange(leaves);
                }
                LogUtil.Write("Looping: " + all.Count);
                foreach (var leaf in all) {
                    if (leaf.Token != null) {
                        LogUtil.Write("Fetching leaf data.");
                        try
                        {
                            var nl = new Panel(leaf.IpV4Address, leaf.Token);
                            var layout = nl.GetLayout().Result;
                            if (layout != null) leaf.Layout = layout;
                        }
                        catch (Exception)
                        {
                            LogUtil.Write("An exception occurred, probably the nanoleaf is unplugged.");
                        }

                        Console.WriteLine("Device: " + JsonConvert.SerializeObject(leaf));
                    }
                }
                DreamData.SetItem<List<NanoData>>("leaves", all);
                message = JsonConvert.SerializeObject(all);
            }

            if (action == "authorizeHue") {
                var doAuth = true;
                var bridges = store.GetItem<List<BridgeData>>("bridges");
                BridgeData bd = null;
                var bridgeInt = -1;

                if (!string.IsNullOrEmpty(value)) {
                    var bCount = 0;
                    foreach (var b in bridges) {
                        if (b.IpV4Address == value) {
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

            if (action == "findHue") {
                var bridges = HueBridge.FindBridges(3);
                if (bridges != null) {
                    store.ReplaceItem("bridges", bridges, true);
                    return new JsonResult(bridges);
                } else {
                    message = "Error: No bridge found.";
                }
            }

            if (action == "authorizeNano") {
                var doAuth = true;
                var leaves = store.GetItem<List<NanoData>>("leaves");
                NanoData bd = null;
                var nanoInt = -1;
                if (!string.IsNullOrEmpty(value)) {
                    var nanoCount = 0;
                    foreach (var n in leaves) {
                        if (n.IpV4Address == value) {
                            bd = n;
                            doAuth = n.Token == null;
                            nanoInt = nanoCount;
                        }
                        nanoCount++;
                    }
                }

                if (doAuth) {
                    var panel = new Panel(value);
                    var appKey = panel.CheckAuth().Result;
                    if (appKey != null && bd != null) {
                        message = "Success: Bridge Linked.";
                        bd.Token = appKey.Token;
                        leaves[nanoInt] = bd;
                        store.ReplaceItem("leaves", leaves, true);
                    } else {
                        message = "Error: Press the link button";
                    }
                } else {
                    message = "Success: NanoLeaf Already Linked.";
                }
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
            var bridgeArray = DreamData.GetItem<List<BridgeData>>("bridges");
            if (bridgeArray.Count == 0 || bridgeArray == null) {
                var newBridges = HueBridge.FindBridges();               
                store.ReplaceItem("bridges", newBridges, true);
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

        private static List<BridgeData> UpdateBridges(List<BridgeData> bridges) {
            var newBridges = HueBridge.FindBridges();

            foreach(var bridge in newBridges) {

            }
            return bridges;
        }
    }
}