using System.Drawing;
using Glimmr.Models.Util;

namespace Glimmr.Models.ColorSource.Ambient.Scene {
	public class SceneSolid : IScene {
		public SceneSolid(Color c) {
			var col = ColorUtil.ColorToHex(c);
			SetColors(new[]{col});
		}
	}
}