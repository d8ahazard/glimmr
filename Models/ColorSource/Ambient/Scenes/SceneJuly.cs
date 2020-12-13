namespace Glimmr.Models.ColorSource.Ambient.Scenes {
    public class SceneJuly : SceneBase {
        public SceneJuly() {
            SetColors(new[] {
                "FF0000", // Red
                "000000", // White
                "0000FF", // Blue
                "000000" // White
            });
            AnimationTime = 10;
            Mode = AnimationMode.Linear;
            Easing = EasingType.Blend;
        }
    }
}