namespace Glimmr.Models.ColorSource.Ambient.Scenes {
    public class SceneRandom : SceneBase {
        public SceneRandom() {
            SetColors(new[] {
                "f5dd02", // Yellow
                "00fcff", // Teal
                "FFFFFF", // White
                "FF0000", // Red
                "00FF00", // Green
                "0000FF", // Blue
                "a500c3" // Purple
            });
            AnimationTime = 20;
            Mode = AnimationMode.RandomAll;
            Easing = EasingType.Blend;
        }
    }
}