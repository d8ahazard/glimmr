using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Hubs;
using Glimmr.Models.DreamScreen;
using Glimmr.Models.DreamScreen.Devices;
using Glimmr.Models.StreamingDevice.Hue;
using Glimmr.Models.StreamingDevice.LIFX;
using Glimmr.Models.StreamingDevice.Nanoleaf;
using Glimmr.Models.StreamingDevice.WLed;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Glimmr.Models.Util {
	public static class ControlUtil {
		
		public static void SetMode(int mode) {
			var ip = IpUtil.GetLocalIpAddress();
			var curMode = DataUtil.GetItem("DeviceMode");
			if (curMode == mode) {
				LogUtil.Write("Old mode is same as new, nothing to do.");
				return;
			}

			DataUtil.SetItem("DeviceMode", mode);
			var payload = new List<byte> {(byte) mode};
			DreamSender.SendUdpWrite(0x01, 0x10, payload.ToArray(), 0x21, 0,
				new IPEndPoint(IPAddress.Parse(ip), 8888));
		}
		
		public static void ResetMode() {
			var curMode = DataUtil.GetItem("DeviceMode");
			if (curMode == 0) return;
			SetMode(0);
			Thread.Sleep(1000);
			SetMode(curMode);
		}
		
		public static async void SetCaptureMode(IHubContext<SocketServer> hubContext, int capMode) {
			LogUtil.Write("Updating capture mode to " + capMode);
			var curMode = DataUtil.GetItem<int>("CaptureMode");
			var dev = DataUtil.GetDeviceData();
			if (curMode == capMode) return;
			DataUtil.SetItem<int>("CaptureMode", capMode);
			var devType = "SideKick";
			if (capMode != 0) {
				devType = "DreamScreen4K";
			}

			SwitchDeviceType(devType, dev);
			DataUtil.SetItem<string>("DevType", devType);
			await TriggerReload(hubContext, JObject.FromObject(dev));
			if (dev.Mode == 0) return;
			ResetMode();
            
		}
		
		private static void SwitchDeviceType(string devType, BaseDevice curDevice) {
            LogUtil.Write("Switching type to " + devType);
            switch (devType) {
                case "SideKick": {
                    var newDevice = new SideKick(curDevice);
                    DataUtil.SetObject("MyDevice", newDevice);
                    DataUtil.InsertDsDevice(newDevice);
                    break;
                }
                case "DreamScreen4K": {
                    var newDevice = new DreamScreen4K(curDevice);
                    DataUtil.SetObject("MyDevice", newDevice);
                    DataUtil.InsertDsDevice(newDevice);
                    break;
                }
                case "Connect": {
                    var newDevice = new Connect(curDevice);
                    DataUtil.SetObject("MyDevice", newDevice);
                    DataUtil.InsertDsDevice(newDevice);
                    break;
                }
            }
        }


        public static async Task<bool> TriggerReload(IHubContext<SocketServer> hubContext, JObject dData) {
            if (dData == null) throw new ArgumentException("invalid jObject");
            if (hubContext == null) throw new ArgumentException("invalid hub context.");
            LogUtil.Write("Reloading data: " + JsonConvert.SerializeObject(dData));
            var tag = (dData["Tag"] ?? "INVALID").Value<string>();
            var id = (dData["_id"] ?? "INVALID").Value<string>();
            dData["Id"] = id;
            if (tag == "INVALID" || id == "INVALID") return false;
            var myDev = DataUtil.GetDeviceData();
            var ipAddress = myDev.IpAddress;
            if (ipAddress == (dData["ipAddress"] ?? "INVALID").Value<string>()) {
                DataUtil.SetItem("MyDevice", dData);
            }
            var groupNumber = (byte) myDev.GroupNumber;
            switch (tag) {
                case "WLed":
                    LogUtil.Write("Updating wled");
                    WLedData existing = DataUtil.GetCollectionItem<WLedData>("Dev_Wled", id);
                    var wData = dData.ToObject<WLedData>();
                    if (wData != null) {
                        if (existing.State.info.leds.rgbw != wData.State.info.leds.rgbw) {
                            LogUtil.Write("Update rgbw type.");
                        }
                        if (existing.State.info.leds.count != wData.State.info.leds.count) {
                            LogUtil.Write("Update count type.");
                        }

                        if (existing.State.state.bri != wData.Brightness) {
                            LogUtil.Write("Update Brightness...");
                        }
                    }

                    DataUtil.InsertCollection<WLedData>("Dev_Wled",wData);
                    await hubContext.Clients.All.SendAsync("wledData", wData);
                    break;
                case "HueBridge":
                    LogUtil.Write("Updating bridge");
                    var bData = dData.ToObject<BridgeData>();
                    DataUtil.InsertCollection<BridgeData>("Dev_Hue", bData);
                    await hubContext.Clients.All.SendAsync("hueData", bData);
                    break;
                case "Lifx":
                    LogUtil.Write("Updating lifx bulb");
                    var lData = dData.ToObject<LifxData>();
                    DataUtil.InsertCollection<LifxData>("Dev_Lifx", lData);
                    await hubContext.Clients.All.SendAsync("lifxData", lData);
                    break;
                case "NanoLeaf":
                    LogUtil.Write("Updating nanoleaf");
                    var nData = dData.ToObject<NanoData>();
                    DataUtil.InsertCollection<NanoData>("Dev_NanoLeaf", nData);
                    await hubContext.Clients.All.SendAsync("nanoData", nData);
                    break;
                case "SideKick":
                    var dsData = dData.ToObject<SideKick>();
                    DataUtil.InsertDsDevice(dsData);
                    break;
                case "Connect":
                    var dcData = dData.ToObject<Connect>();
                    DataUtil.InsertDsDevice(dcData);
                    break;
                case "DreamScreenHd":
                    var dshdData = dData.ToObject<DreamScreenHd>();
                    DataUtil.InsertDsDevice(dshdData);
                    break;
                case "DreamScreen4K":
                    var ds4KData = dData.ToObject<DreamScreen4K>();
                    DataUtil.InsertDsDevice(ds4KData);
                    break;
                case "DreamScreenSolo":
                    var dsSoloData = dData.ToObject<DreamScreenSolo>();
                    DataUtil.InsertDsDevice(dsSoloData);
                    break;
                
            }

            var payload = new List<byte>();
            var utf8 = new UTF8Encoding();
            payload.AddRange(utf8.GetBytes(id));
            DreamSender.SendUdpWrite(0x01, 0x10, payload.ToArray(), 0x21, groupNumber,
                new IPEndPoint(IPAddress.Parse(ipAddress), 8888));
            return true;
        }

        public static async void NotifyClients(IHubContext<SocketServer> hc) {
	        if (hc == null) throw new ArgumentNullException(nameof(hc));
	        await hc.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
	        LogUtil.Write("Sent updated store data via socket.");
        }
        
        public static void TriggerRefresh(IHubContext<SocketServer> hc) {
	        var ipAddress = IpUtil.GetLocalIpAddress();
	        var me = new IPEndPoint(IPAddress.Parse(ipAddress), 8888);
	        DreamSender.SendUdpWrite(0x01, 0x11, new byte[] {0}, 0, 0, me);
	        Thread.Sleep(TimeSpan.FromSeconds(5));
	        NotifyClients(hc);
        }
	}
}