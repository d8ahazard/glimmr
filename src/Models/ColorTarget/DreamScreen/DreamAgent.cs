using Glimmr.Models.Util;
using Glimmr.Services;

namespace Glimmr.Models.ColorTarget.DreamScreen {
	public class DreamAgent : IColorTargetAgent {
		private DreamUtil _du;
		public dynamic CreateAgent(ControlService cs) {
			_du = new DreamUtil(cs.UdpClient);
			return _du;
		}

		public void Dispose() {
			
		}
	}
}