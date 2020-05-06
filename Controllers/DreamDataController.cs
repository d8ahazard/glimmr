using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Accord.Math.Optimization;
using HueDream.Models;
using HueDream.Models.DreamScreen;
using HueDream.Models.DreamScreen.Devices;
using HueDream.Models.StreamingDevice.Hue;
using HueDream.Models.StreamingDevice.LED;
using HueDream.Models.StreamingDevice.LIFX;
using HueDream.Models.StreamingDevice.Nanoleaf;
using HueDream.Models.Util;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HueDream.Controllers {
    [Route("api/[controller]"), ApiController]
    public class DreamDataController : ControllerBase {
        // GET: api/DreamData/getMode
        [Route("getMode")]
        public static int GetMode() {
            var dev = DataUtil.GetDeviceData();
            return dev.Mode;
        }


        // POST: api/DreamData/mode
        [HttpPost("mode")]
        public IActionResult Mode([FromBody] int dMode) {
            SetMode((byte) dMode);
            return Ok(dMode);
        }


        // POST: api/DreamData/capturemode
        [HttpPost("capturemode")]
        public IActionResult CaptureMode([FromBody] int cMode) {
            SetCaptureMode(cMode);
            return Ok(cMode);
        }

        // POST: api/DreamData/devname
        [HttpPost("devname")]
        public IActionResult Devname([FromBody] JObject devInfo) {
            var devIp = (string) devInfo.SelectToken("ip");
            var devName = (string) devInfo.SelectToken("name");
            var devGroup = (int) devInfo.SelectToken("group");
            SetDevName(devIp, devName, devGroup);
            return Ok(devName);
        }

        // POST: api/DreamData/brightness
        [HttpPost("brightness")]
        public IActionResult Brightness([FromBody] JObject bo) {
            SetBrightness(bo);
            return Ok(bo);
        }

        // POST: api/DreamData/camType
        [HttpPost("camType")]
        public IActionResult CamType([FromBody] int cType) {
            DataUtil.SetItem<int>("camType", cType);
            ResetMode();
            return Ok(cType);
        }

        // POST: api/DreamData/vcount
        [HttpPost("vcount")]
        public IActionResult Vcount([FromBody] int count) {
            var ledData = DataUtil.GetItem<LedData>("ledData");
            var capMode = DataUtil.GetItem<int>("captureMode");
            int hCount;
            if (capMode == 0) {
                hCount = ledData.HCountDs;
                ledData.VCountDs = count;
            } else {
                hCount = ledData.HCount;
                ledData.VCount = count;
            }

            ledData.ledCount = hCount * 2 + count * 2;
            DataUtil.SetItem<LedData>("ledData", ledData);
            ResetMode();
            return Ok(count);
        }

        // POST: api/DreamData/hcount
        [HttpPost("hcount")]
        public IActionResult Hcount([FromBody] int count) {
            LedData ledData = DataUtil.GetItem<LedData>("ledData");
            var capMode = DataUtil.GetItem<int>("captureMode");
            int vCount;
            if (capMode == 0) {
                vCount = ledData.VCountDs;
                ledData.HCountDs = count;
            } else {
                vCount = ledData.VCount;
                ledData.HCount = count;
            }

            ledData.LedCount = vCount * 2 + count * 2;
            DataUtil.SetItem<LedData>("ledData", ledData);
            ResetMode();
            return Ok(count);
        }

        // POST: api/DreamData/bridges
        [HttpPost("bridges")]
        public IActionResult PostBridges([FromBody] JArray bData) {
            DataUtil.SetItem("bridges", bData);
            ResetMode();
            return Ok();
        }

        // POST: api/DreamData/leaves
        [HttpPost("leaves")]
        public IActionResult PostLeaves([FromBody] List<NanoData> lData) {
            LogUtil.Write(@"Leaf Post received..." + JsonConvert.SerializeObject(lData));
            DataUtil.SetItem("leaves", lData);
            ResetMode();
            return Ok(lData.Count);
        }

        // POST: api/DreamData/leaf
        [HttpPost("leaf")]
        public IActionResult PostLeaf([FromBody] NanoData lData) {
            LogUtil.Write(@"Leaf Post received..." + JsonConvert.SerializeObject(lData));
            var leaves = DataUtil.GetItem<List<NanoData>>("leaves");
            var leafInt = 0;
            if (leaves.Count > 0) {
                foreach (var leaf in leaves) {
                    if (lData != null)
                        if (leaf.Id == lData.Id) {
                            if (leaf.X != lData.X || leaf.Y != lData.Y) {
                                LogUtil.Write("Recalculating panel positions.");
                                var panelPositions = NanoGroup.CalculatePoints(lData);
                                var newPd = new List<PanelLayout>();
                                var pd = lData.Layout.PositionData;
                                foreach (var pl in pd) {
                                    pl.Sector = panelPositions[pl.PanelId];
                                    newPd.Add(pl);
                                }

                                lData.Layout.PositionData = newPd;
                            }
                            leaves[leafInt] = lData;
                            break;
                        }

                    leafInt++;
                }
            } else {
                leaves = new List<NanoData>();
                leaves.add(lData);
            }

            DataUtil.SetItem("leaves", leaves);
            ResetMode();
            return Ok(lData);
        }

        // POST: api/DreamData/dsIp
        [HttpPost("flipNano")]
        public IActionResult PostIp([FromBody] JObject flipData) {
            var flipDir = (string) flipData.SelectToken("dir");
            var flipVal = (bool) flipData.SelectToken("val");
            var flipDev = (string) flipData.SelectToken("id");
            var leaves = DataUtil.GetItem<List<NanoData>>("leaves");
            var newLeaves = new List<NanoData>();
            NanoData nanoTarget = null;
            foreach (NanoData leaf in leaves) {
                if (leaf.Id == flipDev) {
                    if (flipDir == "h") {
                        leaf.MirrorX = flipVal;
                    } else {
                        leaf.MirrorY = flipVal;
                    }

                    nanoTarget = leaf;
                }

                newLeaves.Add(leaf);
            }

            DataUtil.SetItem("leaves", newLeaves);
            ResetMode();
            return new JsonResult(nanoTarget);
        }


        // POST: api/DreamData/dsIp
        [HttpPost("dsIp")]
        public IActionResult PostIp([FromBody] string dsIp) {
            LogUtil.Write(@"Did it work? " + dsIp);
            DataUtil.SetItem("dsIp", dsIp);
            ResetMode();
            return Ok(dsIp);
        }

        // POST: api/DreamData/dsSidekick
        [HttpPost("dsSidekick")]
        public IActionResult PostSk([FromBody] SideKick skDevice) {
            LogUtil.Write(@"Did it work? " + JsonConvert.SerializeObject(skDevice));
            DataUtil.SetItem("myDevice", skDevice);
            return Ok("ok");
        }

        // POST: api/DreamData/dsConnect
        [HttpPost("dsConnect")]
        public IActionResult PostDevice([FromBody] Connect myDevice) {
            LogUtil.Write(@"Did it work? " + JsonConvert.SerializeObject(myDevice));
            DataUtil.SetItem("myDevice", myDevice);
            return Ok(myDevice);
        }
        
        // POST: api/DreamData/lifxMapping
        [HttpPost("lifxMapping")]
        public IActionResult LifxMapping([FromBody] LifxData myDevice) {
            LogUtil.Write(@"Did it work? " + JsonConvert.SerializeObject(myDevice));
            DataUtil.InsertCollection<LifxData>("lifxBulbs", myDevice);
            return Ok(myDevice);
        }

        [HttpGet("action")]
        public IActionResult Action(string action, string value = "") {
            var message = "Unrecognized action";
            var exInt = 0;
            LogUtil.Write($@"{action} called from Web API.");
            switch (action) {
                case "loadData":
                    return Content(DataUtil.GetStoreSerialized(), "application/json");
                case "refreshDevices":
                    DataUtil.RefreshDevices();
                    return Content(DataUtil.GetStoreSerialized(), "application/json");
                case "findDreamDevices": {
                    List<BaseDevice> dev = DreamDiscovery.Discover().Result;
                    return new JsonResult(dev);
                }
                case "refreshNanoLeaf": {
                    var leaves = DataUtil.GetItem<List<NanoData>>("leaves");
                    NanoData nd = null;
                    var leafInt = 0;
                    foreach (NanoData leaf in leaves) {
                        if (leaf.IpV4Address == value) {
                            nd = leaf;
                            break;
                        }

                        leafInt++;
                    }

                    if (nd != null) {
                        var panel = new NanoGroup(nd.IpV4Address, nd.Token);
                        var layout = panel.GetLayout().Result;
                        panel.Dispose();
                        if (layout == null) return new JsonResult(null);
                        nd.Layout = layout;
                        leaves[leafInt] = nd;
                        DataUtil.SetItem<List<NanoData>>("leaves", leaves);

                        return new JsonResult(layout);
                    }

                    message = "You suck, supply an IP.";
                    break;
                }
                case "findNanoLeaf": {
                    LogUtil.Write("Find Nano Leaf called.");
                    var all = NanoDiscovery.Refresh().Result;
                    message = JsonConvert.SerializeObject(all);
                    break;
                }
                case "authorizeHue": {
                    var doAuth = true;
                    BridgeData bd = null;
                    if (!string.IsNullOrEmpty(value)) {
                        bd = DataUtil.GetCollectionItem<BridgeData>("bridges", value);
                        if (bd == null) return new JsonResult(null);
                        if (bd.Key != null && bd.User != null) doAuth = false;
                    } else {
                        doAuth = false;
                    }

                    if (!doAuth) return new JsonResult(bd);
                    var appKey = HueDiscovery.CheckAuth(value).Result;
                    if (appKey == null) return new JsonResult(bd);
                    bd.Key = appKey.StreamingClientKey;
                    bd.User = appKey.Username;
                    DataUtil.InsertCollection<BridgeData>("bridges", bd);
                    return new JsonResult(bd);
                }
                case "findHue": {
                    var bridges = HueDiscovery.Discover();
                    DataUtil.SetItem("bridges", bridges);
                    return new JsonResult(bridges);
                }
                case "authorizeNano": {
                    var doAuth = true;
                    var leaves = DataUtil.GetItem<List<NanoData>>("leaves");
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
                        var panel = new NanoGroup(value);
                        var appKey = panel.CheckAuth().Result;
                        if (appKey != null && bd != null) {
                            bd.Token = appKey.Token;
                            leaves[nanoInt] = bd;
                            DataUtil.SetItem("leaves", leaves);
                        }
                        panel.Dispose();
                    }
                    return new JsonResult(bd);
                }
            }

            LogUtil.Write(message);
            return new JsonResult(message);
        }

        // GET: api/DreamData/json
        [HttpGet("json")]
        public IActionResult GetJson() {
            LogUtil.Write(@"GetJson Called.");
            var store = DataUtil.GetStore();
            var bridgeArray = DataUtil.GetCollection<BridgeData>("bridges");
            if (bridgeArray.Count == 0) {
                var newBridges = HueDiscovery.Discover();
                store.ReplaceItem("bridges", newBridges, true);
            }

            if (store.GetItem("dsIp") == "0.0.0.0") {
                var devices = DreamDiscovery.Discover();
                store.ReplaceItem("devices", devices);
            }

            store.Dispose();
            return Content(DataUtil.GetStoreSerialized(), "application/json");
        }


        private static void ResetMode() {
            var myDev = DataUtil.GetDeviceData();
            var curMode = myDev.Mode;
            if (curMode == 0) return;
            SetMode(0);
            Thread.Sleep(1000);
            SetMode(curMode);

        }

        private static void SetMode(int mode) {
            var newMode = ByteUtils.IntByte(mode);
            var myDev = DataUtil.GetDeviceData();
            var curMode = myDev.Mode;
            if (curMode == newMode) return;
            LogUtil.Write("Updating mode to " + mode);
            var ipAddress = myDev.IpAddress;
            var groupNumber = (byte) myDev.GroupNumber;

            var groupSend = false;
            byte mFlag = 0x11;
            if (ipAddress == "255.255.255.0") {
                groupSend = true;
            } else {
                mFlag = 0x21;
            }

            DreamSender.SendUdpWrite(0x03, 0x01, new[] {newMode}, mFlag, groupNumber,
                new IPEndPoint(IPAddress.Parse(ipAddress), 8888), groupSend);
        }


        private void SetCaptureMode(int capMode) {
            LogUtil.Write("Updating capture mode to " + capMode);
            var curMode = DataUtil.GetItem<int>("captureMode");
            var dev = DataUtil.GetDeviceData();
            if (curMode == capMode) return;
            var colorMode = dev.Mode;
            DataUtil.SetItem<int>("captureMode", capMode);
            var devType = "SideKick";
            if (capMode != 0 && curMode == 0) {
                devType = "DreamVision";
                SwitchDeviceType(devType, dev);
            }

            if (capMode == 0) {
                SwitchDeviceType(devType, dev);
            }

            DataUtil.SetItem<string>("devType", devType);
            if (colorMode == 0) return;
            SetMode(0);
            SetMode(colorMode);
        }

        private static void SwitchDeviceType(string devType, BaseDevice curDevice) {
            if (devType == "DreamVision") {
                var newDevice = new DreamVision();
                newDevice.SetDefaults();
                newDevice.Id = curDevice.Id;
                newDevice.Name = curDevice.Name;
                newDevice.IpAddress = curDevice.IpAddress;
                newDevice.Brightness = curDevice.Brightness;
                newDevice.GroupNumber = curDevice.GroupNumber;
                newDevice.flexSetup = curDevice.flexSetup;
                newDevice.Saturation = curDevice.Saturation;
                newDevice.Mode = curDevice.Mode;
                DataUtil.SetItem("myDevice", newDevice);
            } else {
                var newDevice = new SideKick();
                newDevice.SetDefaults();
                newDevice.Id = curDevice.Id;
                newDevice.Name = curDevice.Name;
                newDevice.IpAddress = curDevice.IpAddress;
                newDevice.Brightness = curDevice.Brightness;
                newDevice.GroupNumber = curDevice.GroupNumber;
                newDevice.flexSetup = curDevice.flexSetup;
                newDevice.Saturation = curDevice.Saturation;
                newDevice.Mode = curDevice.Mode;
                DataUtil.SetItem("myDevice", newDevice);
            }
        }

        private static void SetDevName(string ipAddress, string name, int group) {
            var groupNumber = (byte) group;
            bool groupSend = false;
            byte mFlag = 0x11;
            if (ipAddress == "255.255.255.0") {
                groupSend = true;
            } else {
                mFlag = 0x21;
            }

            LogUtil.Write($@"Setting device name for {ipAddress} to {name}.");
            DreamSender.SendUdpWrite(0x01, 0x07, ByteUtils.StringBytePad(name, 16).ToArray(), mFlag, groupNumber,
                new IPEndPoint(IPAddress.Parse(ipAddress), 8888), groupSend);
            List<JObject> devices = DataUtil.GetItem<List<JObject>>("devices");
            List<JObject> newDevices = new List<JObject>();
            foreach(var dev in devices) {
                var dIp = (string) dev.SelectToken("ipAddress");
                if (dIp == ipAddress) {
                    dev["name"] = ByteUtils.StringBytePad(name, 16).ToArray();
                    LogUtil.Write("We dun got us an updated device: " + JsonConvert.SerializeObject(dev));
                }
                newDevices.Add(dev);
            }
            DataUtil.SetItem("devices", newDevices);
        }

        private static void SetBrightness(JObject dData) {
            if (dData == null) throw new ArgumentException("invalid jobject");
            var tag = (dData["tag"] ?? "INVALID").Value<string>();
            var id = (dData["id"] ?? "INVALID").Value<string>();
            var brightness = (dData["brightness"] ?? -1).Value<int>();
            LogUtil.Write($"Setting brightness for {tag} {id} to {brightness}.");
            switch (tag) {
                case "Hue":
                    var bridge = DataUtil.GetCollectionItem<BridgeData>("bridges", id);
                    bridge.MaxBrightness = brightness;
                    DataUtil.InsertCollection<BridgeData>("bridges", bridge);
                    break;
                case "Lifx":
                    var bulb = DataUtil.GetCollectionItem<LifxData>("lifxBulbs", id);
                    bulb.MaxBrightness = brightness;
                    DataUtil.InsertCollection<LifxData>("lifxBulbs", bulb);
                    break;
                case "NanoLeaf":
                    var panel = DataUtil.GetCollectionItem<NanoData>("leaves", id);
                    panel.MaxBrightness = brightness;
                    DataUtil.InsertCollection<NanoData>("leaves", panel);
                    break;
                default:
                    var myDev = DataUtil.GetDeviceData();
                    var ipAddress = myDev.IpAddress;
                    var groupNumber = (byte) myDev.GroupNumber;
                    var groupSend = false;
                    byte mFlag = 0x11;
                    if (ipAddress == "255.255.255.0") {
                        groupSend = true;
                    } else {
                        mFlag = 0x21;
                    }

                    DreamSender.SendUdpWrite(0x03, 0x02, new[] {ByteUtils.IntByte(brightness)}, mFlag, groupNumber,
                        new IPEndPoint(IPAddress.Parse(ipAddress), 8888), groupSend);
                    break;
            }
            ResetMode();
        }
    }
}