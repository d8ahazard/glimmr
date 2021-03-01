using System.Runtime.InteropServices;
using Colore;
using Glimmr.Services;

namespace Glimmr.Models.ColorTarget.Razer {
	public class RazerAgent : IColorTargetAgent {

		private IChroma _chroma;
		public dynamic CreateAgent(ControlService cs) {
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return null;
			_chroma = ColoreProvider.CreateNativeAsync().Result;
			
			return _chroma;

		}

		public void Dispose() {
			_chroma.Dispose();
		}
	}
}