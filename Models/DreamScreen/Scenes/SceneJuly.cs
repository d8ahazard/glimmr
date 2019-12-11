namespace HueDream.Models.DreamScreen.Scenes {
    public class SceneJuly : SceneBase {
        public SceneJuly() {
            SetColors(new[] {
                "FF0000", // Red
                "0000FF", // Blue
                "000000" // White
            });
            AnimationTime = 1;
            Mode = AnimationMode.Random;
            Easing = EasingType.Blend;
        }
    }
}