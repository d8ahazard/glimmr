#region

using System;
using System.Threading.Tasks;
using Glimmr.Hubs;
using Glimmr.Models.StreamingDevice.Dreamscreen;
using Glimmr.Models.StreamingDevice.Hue;
using Glimmr.Models.StreamingDevice.LIFX;
using Glimmr.Models.StreamingDevice.Nanoleaf;
using Glimmr.Models.StreamingDevice.WLED;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#endregion

namespace Glimmr.Models.Util {
	public static class ControlUtil {
		public static async void SetCaptureMode(IHubContext<SocketServer> hubContext, int capMode) {
			LogUtil.Write("Updating capture mode to " + capMode);
			var curMode = DataUtil.GetItem<int>("CaptureMode");
			var dev = DataUtil.GetDeviceData();
			if (curMode == capMode) return;
			DataUtil.SetItem<int>("CaptureMode", capMode);
			var devType = "SideKick";
			if (capMode != 0) devType = "Dreamscreen4K";

			SwitchDeviceType(devType, dev);
			DataUtil.SetItem<string>("DevType", devType);
			await TriggerReload(hubContext, JObject.FromObject(dev));
			if (dev.Mode == 0) return;
		}

		private static void SwitchDeviceType(string devType, DreamData curDevice) {
			LogUtil.Write("Switching type to " + devType);
			curDevice.DeviceTag = devType;
			DataUtil.InsertCollection<DreamData>("Dev_Dreamscreen", curDevice);
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
			if (ipAddress == (dData["ipAddress"] ?? "INVALID").Value<string>()) DataUtil.SetItem("MyDevice", dData);
			var groupNumber = (byte) myDev.GroupNumber;
			try {
				switch (tag) {
					case "Wled":
						LogUtil.Write("Updating wled");
						WledData existing = DataUtil.GetCollectionItem<WledData>("Dev_Wled", id);
						var wData = dData.ToObject<WledData>();
						if (wData != null) {
							if (existing.State.info.leds.rgbw != wData.State.info.leds.rgbw)
								LogUtil.Write("Update rgbw type.");

							if (existing.State.info.leds.count != wData.State.info.leds.count)
								LogUtil.Write("Update count type.");

							if (existing.State.state.bri != wData.Brightness) LogUtil.Write("Update Brightness...");
						}

						DataUtil.InsertCollection<WledData>("Dev_Wled", wData);
						await hubContext.Clients.All.SendAsync("wledData", wData);
						break;
					case "HueBridge":
						LogUtil.Write("Updating bridge");
						var bData = dData.ToObject<HueData>();
						DataUtil.InsertCollection<HueData>("Dev_Hue", bData);
						await hubContext.Clients.All.SendAsync("hueData", bData);
						break;
					case "Lifx":
						LogUtil.Write("Updating lifx bulb");
						var lData = dData.ToObject<LifxData>();
						DataUtil.InsertCollection<LifxData>("Dev_Lifx", lData);
						await hubContext.Clients.All.SendAsync("lifxData", lData);
						break;
					case "Nanoleaf":
						LogUtil.Write("Updating nanoleaf");
						var nData = dData.ToObject<NanoleafData>();
						DataUtil.InsertCollection<NanoleafData>("Dev_Nanoleaf", nData);
						await hubContext.Clients.All.SendAsync("nanoData", nData);
						break;
					case "Dreamscreen":
						var dsData = dData.ToObject<DreamData>();
						DataUtil.InsertCollection<DreamData>("Dev_Dreamscreen", dsData);
						break;
				}
			} catch (Exception e) {
				LogUtil.Write("Got me an exception here: " + e.Message, "WARN");
			}

			return true;
		}

		public static async void NotifyClients(IHubContext<SocketServer> hc) {
			if (hc == null) throw new ArgumentNullException(nameof(hc));
			await hc.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
			LogUtil.Write("Sent updated store data via socket.");
		}
	}
}