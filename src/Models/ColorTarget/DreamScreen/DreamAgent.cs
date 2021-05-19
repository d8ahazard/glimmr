using DreamScreenNet;
using Glimmr.Services;

namespace Glimmr.Models.ColorTarget.DreamScreen {
	public class DreamAgent : IColorTargetAgent {
		private DreamScreenClient _du;

		public dynamic CreateAgent(ControlService cs) {
			_du = new DreamScreenClient(cs.UdpClient);
			return _du;
		}

		public void Dispose() {
		}
	}
}