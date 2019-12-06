namespace HueDream.DreamScreen.Scenes {
    public class SceneHoliday : SceneBase {
        public SceneHoliday() {
            SetColors(new[] {
                "FF0000", // Red
                "00FF00" // Green
            });
            AnimationTime = 1.75;
            Mode = AnimationMode.Linear;
            Easing = EasingType.Blend;
        }
    }
}