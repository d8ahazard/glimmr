using Glimmr.Services;
using LifxNet;
using Q42.HueApi;

namespace Glimmr.Models.ColorTarget.LIFX {
	public class LifxAgent : IColorTargetAgent {
		
		public dynamic CreateAgent(ControlService cs) {
			var lc = LifxClient.CreateAsync().Result;

			return lc;
		}
	}
}