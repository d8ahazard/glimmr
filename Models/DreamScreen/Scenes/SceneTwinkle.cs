namespace HueDream.Models.DreamScreen.Scenes {
    public class SceneTwinkle : SceneBase {
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