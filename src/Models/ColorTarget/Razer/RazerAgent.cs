using System.Runtime.InteropServices;
using Colore;
using Glimmr.Models.Util;
using Glimmr.Services;
using Microsoft.AspNetCore.Razor.Language;

namespace Glimmr.Models.ColorTarget.Razer {
	public class RazerAgent : IColorTargetAgent {
		
		public dynamic CreateAgent(ControlService cs) {
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return null;
			return ColoreProvider.CreateNativeAsync().Result;
		}
	}
}