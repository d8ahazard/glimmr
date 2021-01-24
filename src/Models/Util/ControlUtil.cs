#region

using System;
using System.Threading.Tasks;
using Glimmr.Hubs;
using Glimmr.Models.ColorTarget.DreamScreen;
using Glimmr.Models.ColorTarget.Hue;
using Glimmr.Models.ColorTarget.LIFX;
using Glimmr.Models.ColorTarget.Nanoleaf;
using Glimmr.Models.ColorTarget.Wled;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

#endregion

namespace Glimmr.Models.Util {
	public static class ControlUtil {
		public static async Task SetCaptureMode(IHubContext<SocketServer> hubContext, int capMode) {
			Log.Debug("Updating capture mode to " + capMode);
			var curMode = DataUtil.GetItem<int>("CaptureMode");
			var dev = DataUtil.GetDeviceData();
			if (curMode == capMode) return;
			DataUtil.SetItem<int>("CaptureMode", capMode);
			var devType = "SideKick";
			if (capMode != 0) devType = "Dreamscreen4K";

			SwitchDeviceType(devType, dev);
			DataUtil.SetItem<string>("DevType", devType);
			await TriggerReload(hubContext, JObject.FromObject(dev));
		}

		private static void SwitchDeviceType(string devType, DreamData curDevice) {
			Log.Debug("Switching type to " + devType);
			curDevice.DeviceTag = devType;
			DataUtil.InsertCollection<DreamData>("Dev_Dreamscreen", curDevice);
		}


		public static async Task<bool> TriggerReload(IHubContext<SocketServer> hubContext, JObject dData) {
			if (dData == null) throw new ArgumentException("invalid jObject");
			if (hubContext == null) throw new ArgumentException("invalid hub context.");
			Log.Debug("Reloading data: " + JsonConvert.SerializeObject(dData));
			var tag = (dData["Tag"] ?? "INVALID").Value<string>();
			var id = (dData["_id"] ?? "INVALID").Value<string>();
			dData["Id"] = id;
			if (tag == "INVALID" || id == "INVALID") return false;
			try {
				switch (tag) {
					case "Wled":
						var wData = dData.ToObject<WledData>();
						if (wData != null) {
							Log.Debug("Updating wled");
							DataUtil.InsertCollection<WledData>("Dev_Wled", wData);
							await hubContext.Clients.All.SendAsync("wledData", wData);
						}
						break;
					case "HueBridge":
						var bData = dData.ToObject<HueData>();
						if (bData != null) {
							Log.Debug("Updating bridge");
							DataUtil.InsertCollection<HueData>("Dev_Hue", bData);
							await hubContext.Clients.All.SendAsync("hueData", bData);
						}
						break;
					case "Lifx":
						var lData = dData.ToObject<LifxData>();
						if (lData != null) {
							Log.Debug("Updating lifx bulb");
							DataUtil.InsertCollection<LifxData>("Dev_Lifx", lData);
							await hubContext.Clients.All.SendAsync("lifxData", lData);
						}
						break;
					case "Nanoleaf":
						var nData = dData.ToObject<NanoleafData>();
						if (nData != null) {
							Log.Debug("Updating nanoleaf");
							DataUtil.InsertCollection<NanoleafData>("Dev_Nanoleaf", nData);
							await hubContext.Clients.All.SendAsync("nanoData", nData);
						}
						break;
					case "Dreamscreen":
						var dsData = dData.ToObject<DreamData>();
						if (dsData != null) {
							Log.Debug("Updating Dreamscreen");
							DataUtil.InsertCollection<DreamData>("Dev_Dreamscreen", dsData);
							if (dsData.IpAddress == IpUtil.GetLocalIpAddress()) {
								DataUtil.SetDeviceData(dsData);
							}
							await hubContext.Clients.All.SendAsync("dreamData", dsData);
						}
						break;
				}
			} catch (Exception e) {
				Log.Warning("We have an exception: ", e);
			}

			return true;
		}

		public static void NotifyClients(IHubContext<SocketServer> hc) {
			if (hc == null) throw new ArgumentNullException(nameof(hc));
			hc.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
			Log.Debug("Sent updated store data via socket.");
		}

		
	}
}