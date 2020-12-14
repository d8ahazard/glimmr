namespace Glimmr.Models.ColorSource.Ambient.Scene {
    public class SceneOcean : IScene {
        public SceneOcean() {
            SetColors(new[] {
                "02c676", // Sea Green
                "2dbee7", // Sky Blue
                "aaaaaa", // White
                "00beff", // Deeper blue
                "00a763" // Deeper green
            });
            AnimationTime = 5;
            Mode = AnimationMode.Linear;
            Easing = EasingType.Blend;
        }
    }
}