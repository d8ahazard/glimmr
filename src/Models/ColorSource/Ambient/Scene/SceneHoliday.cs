namespace Glimmr.Models.ColorSource.Ambient.Scene {
    public class SceneHoliday : IScene {
        public SceneHoliday() {
            SetColors(new[] {
                "FF0000", // Red
                "00FF00", // Green
                "FF0000" // Red
            });
            AnimationTime = 3;
            Mode = AnimationMode.Random;
            Easing = EasingType.Blend;
        }
    }
}