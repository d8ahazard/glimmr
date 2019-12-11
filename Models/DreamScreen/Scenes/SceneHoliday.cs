namespace HueDream.Models.DreamScreen.Scenes {
    public class SceneHoliday : SceneBase {
        public SceneHoliday() {
            SetColors(new[] {
                "FF0000", // Red
                "00FF00", // Green
                "FF0000" // Red
            });
            AnimationTime = 1.75;
            Mode = AnimationMode.Random;
            Easing = EasingType.Blend;
        }
    }
}