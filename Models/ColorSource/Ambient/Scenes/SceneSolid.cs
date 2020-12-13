namespace Glimmr.Models.ColorSource.Ambient.Scenes {
	public class SceneSolid : SceneBase {
		public SceneSolid(string color) {
			SetColors(new []{color});
		}
	}
}