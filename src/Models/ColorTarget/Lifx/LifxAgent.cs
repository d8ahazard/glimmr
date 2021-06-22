using Glimmr.Services;
using LifxNetPlus;

namespace Glimmr.Models.ColorTarget.Lifx {
	public class LifxAgent : IColorTargetAgent {
		private LifxClient? _lc;

		public dynamic? CreateAgent(ControlService cs) {
			_lc = LifxClient.CreateAsync().Result;
			return _lc;
		}

		public void Dispose() {
			_lc?.Dispose();
		}
	}
}