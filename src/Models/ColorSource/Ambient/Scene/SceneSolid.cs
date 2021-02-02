using System.Drawing;
using Glimmr.Models.Util;
using Serilog;

namespace Glimmr.Models.ColorSource.Ambient.Scene {
	public class SceneSolid : IScene {
		
		public SceneSolid(Color c) {
			var col = ColorUtil.ColorToHex(c);
			Log.Debug("Setting solid color: " + col);
			SetColors(new[]{col});
			AnimationTime = 1;
		}
	}
}