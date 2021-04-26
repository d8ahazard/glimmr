#region

using System;
using System.Threading.Tasks;
using Glimmr.Hubs;
using Glimmr.Models.ColorTarget;
using Glimmr.Models.ColorTarget.DreamScreen;
using Glimmr.Models.ColorTarget.Hue;
using Glimmr.Models.ColorTarget.Lifx;
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
			var curMode = DataUtil.GetItem<int>("CaptureMode");
			if (curMode == capMode) return;
			DataUtil.SetItem("CaptureMode", capMode);
			var devType = "SideKick";
			if (capMode != 0) devType = "Dreamscreen4K";

			DataUtil.SetItem("DevType", devType);
		}

		private static async Task SwitchDeviceType(string devType, DreamScreenData curDevice) {
			curDevice.DeviceTag = devType;
			await DataUtil.InsertCollection<DreamScreenData>("Dev_Dreamscreen", curDevice);
		}


		public static async Task TriggerReload(IHubContext<SocketServer> hubContext, JObject dData) {
			if (dData == null) throw new ArgumentException("invalid jObject");
			if (hubContext == null) throw new ArgumentException("invalid hub context.");
			var dO = dData.ToObject<IColorTargetData>();
			await DataUtil.AddDeviceAsync(dO);
			await hubContext.Clients.All.SendAsync("Device", dO);
		}

		public static async Task NotifyClients(IHubContext<SocketServer> hc) {
			if (hc == null) throw new ArgumentNullException(nameof(hc));
			await hc.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
		}

		public static async Task AddDevice(IColorTargetData data) {
			
		}
		
	}
}