using System;
using Corsair.CUE.SDK;
using Glimmr.Services;

namespace Glimmr.Models.ColorTarget.Corsair {
	public class CorsairAgent {
		public dynamic CreateAgent(ControlService cs) {
			try {
				CUESDK.CorsairPerformProtocolHandshake();
				CUESDK.CorsairRequestControl(CorsairAccessMode.CAM_ExclusiveLightingControl);
			} catch (Exception e) {
				Console.WriteLine("Handshake exception: " + e.Message);
			}

			return null;
		}

		public void Dispose() {
			CUESDK.CorsairReleaseControl(CorsairAccessMode.CAM_ExclusiveLightingControl);
		}
	}
}