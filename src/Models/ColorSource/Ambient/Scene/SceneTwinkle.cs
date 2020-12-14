namespace Glimmr.Models.ColorSource.Ambient.Scene {
    public class SceneTwinkle : IScene {
        public SceneTwinkle() {
            SetColors(new[] {
                "AAAAAA",
                "000000"
            });
            AnimationTime = 1;
            Mode = AnimationMode.Random;
            Easing = EasingType.Blend;
        }
    }
}