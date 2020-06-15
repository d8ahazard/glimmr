using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HueDream.Models.DreamScreen;
using HueDream.Models.DreamScreen.Devices;
using HueDream.Models.LED;
using HueDream.Models.StreamingDevice.Hue;
using HueDream.Models.StreamingDevice.LIFX;
using HueDream.Models.StreamingDevice.Nanoleaf;
using HueDream.Models.Util;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Q42.HueApi;
using Serilog;

namespace HueDream.Controllers {
    [Route("api/[controller]"), ApiController]
    public class DreamDataController : ControllerBase {
        private HueClient hueClient;
        // GET: api/DreamData/getMode
        [Route("getMode")]
        public static int GetMode() {
            var dev = DataUtil.GetDeviceData();
            return dev.Mode;
        }


        // POST: api/DreamData/mode
        [HttpPost("mode")]
        public IActionResult DevMode([FromBody] JObject modeObj) {
            SetMode(modeObj);
            return Ok(modeObj);
        }

        [HttpPost("updateDs")]
        public IActionResult UpdateDs([FromBody] JObject dsSetting) {
            if (dsSetting == null) throw new ArgumentException("Invalid Jobject.");
            var id = (dsSetting["id"] ?? "").Value<string>();
            var property = (dsSetting["property"] ?? "").Value<string>();
            var value = (dsSetting["value"] ?? "").Value<string>();
            LogUtil.Write($"We got our stuff: {id}, {property}, {value}");
            DreamSender.SendMessage(property, value, id);
            return Ok();
        }

        // POST: api/DreamData/updateDevice
        [HttpPost("updateDevice")]
        public IActionResult UpdateDevice([FromBody] JObject dData) {
            var res = TriggerReload(dData);
            return Ok(res);
        }
        
        // POST: api/DreamData/updateDevice
        [HttpPost("updateData")]
        public IActionResult UpdateData([FromBody] JObject dData) {
            var res = TriggerReload(dData);
            return Ok(res);
        }

        // POST: api/DreamData/capturemode
        [HttpPost("capturemode")]
        public IActionResult CaptureMode([FromBody] int cMode) {
            SetCaptureMode(cMode);
            return Ok(cMode);
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


        [HttpGet("action")]
        public IActionResult Action(string action, string value = "") {
            var message = "Unrecognized action";
            LogUtil.Write($@"{action} called from Web API.");
            switch (action) {
                case "loadData":
                    return Content(DataUtil.GetStoreSerialized(), "application/json");
                case "refreshDevices":
                    var res = Task.Run(() => {
                        DataUtil.RefreshDevices();
                        Thread.Sleep(5000);
                        return DataUtil.GetStoreSerialized();
                    });
                    return Content(res.Result, "application/json");
                case "authorizeHue": {
                    LogUtil.Write("AuthHue called, for real.");
                    var doAuth = true;
                    BridgeData bd = null;
                    if (!string.IsNullOrEmpty(value)) {
                        LogUtil.Write("Value is good: " + value);
                        bd = DataUtil.GetCollectionItem<BridgeData>("bridges", value);
                        LogUtil.Write("BD: " + JsonConvert.SerializeObject(bd));
                        if (bd == null) {
                            LogUtil.Write("Null bridge retrieved.");
                            return new JsonResult(null);
                        }

                        if (bd.Key != null && bd.User != null) {
                            LogUtil.Write("Bridge is already authorized.");
                            doAuth = false;
                        }
                    } else {
                        LogUtil.Write("Null value.", "WARN");
                        doAuth = false;
                    }

                    if (!doAuth) {
                        LogUtil.Write("No auth, returning existing data.");
                        return new JsonResult(bd);
                    }
                    LogUtil.Write("Trying to retrieve appkey...");
                    var appKey = HueDiscovery.CheckAuth(bd.IpAddress).Result;
                    if (appKey == null) {
                        LogUtil.Write("Error retrieving app key.");
                        return new JsonResult(bd);
                    }
                    bd.Key = appKey.StreamingClientKey;
                    bd.User = appKey.Username;
                    LogUtil.Write("We should be authorized, returning.");
                    DataUtil.InsertCollection<BridgeData>("bridges", bd);
                    return new JsonResult(bd);
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


        private static void SetMode(JObject modeObj) {
            var myDev = DataUtil.GetDeviceData();
            var ipAddress = myDev.IpAddress;
            var groupNumber = (byte) myDev.GroupNumber;
            var newMode = (byte) (modeObj["mode"] ?? "").Value<int>();
            var id = (modeObj["id"] ?? "").Value<string>();
            var tag = (modeObj["tag"] ?? "").Value<string>();
            var groupSend = false;
            byte mFlag = 0x21;
            LogUtil.Write("Setting mode for: " + JsonConvert.SerializeObject(modeObj));
            switch (tag) {
                case "HueBridge":
                case "Lifx":
                case "NanoLeaf":
                    if (myDev.Mode == newMode) return;
                    myDev.Mode = newMode;
                    DataUtil.SetItem("myDevice", myDev);
                    break;
                case "Group":
                    groupNumber = (byte) int.Parse(id, CultureInfo.InvariantCulture);
                    ipAddress = "255.255.255.0";
                    groupSend = true;
                    mFlag = 0x11;
                    break;
                default:
                    var dev = DataUtil.GetDreamDevices().Find(e => e.Id == id);
                    if (dev != null) {
                        ipAddress = dev.IpAddress;
                        if (dev.Mode == newMode) return;
                        dev.Mode = newMode;
                        DataUtil.InsertCollection("devices", dev);
                    }

                    break;
            }

            DreamSender.SendUdpWrite(0x03, 0x01, new[] {newMode}, mFlag, groupNumber,
                new IPEndPoint(IPAddress.Parse(ipAddress), 8888), groupSend);
        }

        private static void SetMode(int mode) {
            var newMode = ByteUtils.IntByte(mode);
            var myDev = DataUtil.GetDeviceData();
            var curMode = myDev.Mode;
            if (curMode == newMode) {
                LogUtil.Write("Old mode is same as new, nothing to do.");
                return;
            }
            LogUtil.Write("Updating mode to " + mode);
            var ipAddress = myDev.IpAddress;
            var groupNumber = (byte) myDev.GroupNumber;

            DreamSender.SendUdpWrite(0x03, 0x01, new[] {newMode}, 0x21, groupNumber,
                new IPEndPoint(IPAddress.Parse(ipAddress), 8888));
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
                devType = "DreamScreen4K";
            }

            SwitchDeviceType(devType, dev);
            DataUtil.SetItem<string>("devType", devType);
            if (colorMode == 0) return;
            SetMode(0);
            SetMode(colorMode);
        }

        private static void SwitchDeviceType(string devType, BaseDevice curDevice) {
            if (devType == "SideKick") {
                var newDevice = new SideKick(curDevice);
                DataUtil.SetItem("myDevice", newDevice);
            } else if (devType == "DreamScreen4K") {
                var newDevice = new DreamScreen4K(curDevice);
                DataUtil.SetItem("myDevice", newDevice);
            } else if (devType == "Connect") {
                var newDevice = new Connect(curDevice);
                DataUtil.SetItem("myDevice", newDevice);
            }
        }


        private static bool TriggerReload(JObject dData) {
            if (dData == null) throw new ArgumentException("invalid jobject");
            var tag = (dData["tag"] ?? "INVALID").Value<string>();
            var id = (dData["id"] ?? "INVALID").Value<string>();
            if (tag == "INVALID" || id == "INVALID") return false;
            var myDev = DataUtil.GetDeviceData();
            var ipAddress = myDev.IpAddress;
            var groupNumber = (byte) myDev.GroupNumber;
            switch (tag) {
                case "HueBridge":
                    LogUtil.Write("Updating bridge");
                    DataUtil.InsertCollection<BridgeData>("bridges", dData.ToObject<BridgeData>());
                    break;
                case "Lifx":
                    LogUtil.Write("Updating lifx bulb");
                    DataUtil.InsertCollection<LifxData>("lifxBulbs", dData.ToObject<LifxData>());
                    break;
                case "NanoLeaf":
                    LogUtil.Write("Updating nanoleaf");
                    DataUtil.InsertCollection<NanoData>("leaves", dData.ToObject<NanoData>());
                    break;
            }

            var payload = new List<byte>();
            var utf8 = new UTF8Encoding();
            payload.AddRange(utf8.GetBytes(id));
            DreamSender.SendUdpWrite(0x01, 0x10, payload.ToArray(), 0x21, groupNumber,
                new IPEndPoint(IPAddress.Parse(ipAddress), 8888));
            return true;
        }

        private static void SetBrightness(JObject dData) {
            if (dData == null) throw new ArgumentException("invalid jobject");
            var tag = (dData["tag"] ?? "INVALID").Value<string>();
            var id = (dData["id"] ?? "INVALID").Value<string>();
            var brightness = (dData["brightness"] ?? -1).Value<int>();
            LogUtil.Write($"Setting brightness for {tag} {id} to {brightness}.");
            var myDev = DataUtil.GetDeviceData();
            var ipAddress = myDev.IpAddress;
            var groupNumber = (byte) myDev.GroupNumber;
            var sendRemote = false;
            var remoteId = "";
            switch (tag) {
                case "Hue":
                    var bridge = DataUtil.GetCollectionItem<BridgeData>("bridges", id);
                    bridge.MaxBrightness = brightness;
                    DataUtil.InsertCollection<BridgeData>("bridges", bridge);
                    sendRemote = true;
                    remoteId = bridge.Id;
                    break;
                case "Lifx":
                    var bulb = DataUtil.GetCollectionItem<LifxData>("lifxBulbs", id);
                    bulb.MaxBrightness = brightness;
                    DataUtil.InsertCollection<LifxData>("lifxBulbs", bulb);
                    sendRemote = true;
                    remoteId = bulb.Id;
                    break;
                case "NanoLeaf":
                    var panel = DataUtil.GetCollectionItem<NanoData>("leaves", id);
                    panel.MaxBrightness = brightness;
                    DataUtil.InsertCollection<NanoData>("leaves", panel);
                    sendRemote = true;
                    remoteId = panel.Id;
                    break;
                default:
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

            if (!sendRemote) return;
            var payload = new List<byte> {ByteUtils.IntByte(brightness)};
            var utf8 = new UTF8Encoding();
            payload.AddRange(utf8.GetBytes(remoteId));
            DreamSender.SendUdpWrite(0x01, 0x10, payload.ToArray(), 0x21, groupNumber,
                new IPEndPoint(IPAddress.Parse(ipAddress), 8888));
        }
    }
}