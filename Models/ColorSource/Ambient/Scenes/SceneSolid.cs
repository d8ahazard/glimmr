using System.Drawing;
using Glimmr.Models.Util;

namespace Glimmr.Models.ColorSource.Ambient.Scenes {
	public class SceneSolid : SceneBase {
		public SceneSolid(Color c) {
			var col = ColorUtil.ColorToHex(c);
			SetColors(new[]{col});
		}
	}
}