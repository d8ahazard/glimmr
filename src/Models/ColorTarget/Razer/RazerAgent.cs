using Colore;
using Glimmr.Services;

namespace Glimmr.Models.ColorTarget.Razer {
	public class RazerAgent : IColorTargetAgent {
		
		public dynamic CreateAgent(ControlService cs) {
			return ColoreProvider.CreateNativeAsync().Result;
		}
	}
}