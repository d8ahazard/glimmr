using System;
using System.Runtime.InteropServices;
using Colore;
using Glimmr.Services;
using Serilog;

namespace Glimmr.Models.ColorTarget.Razer {
	public class RazerAgent : IColorTargetAgent {

		private IChroma _chroma;
		public dynamic CreateAgent(ControlService cs) {
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return null;
			try {
				_chroma = ColoreProvider.CreateNativeAsync().Result;
			} catch (Exception e) {
				Log.Debug("Chroma init error, probably no SDK installed...");
			}

			return _chroma;
		}

		public void Dispose() {
			_chroma?.Dispose();
		}
	}
}