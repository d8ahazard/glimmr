namespace Glimmr.Models.ColorSource.Ambient.Scene {
    public class SceneJuly : IScene {
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