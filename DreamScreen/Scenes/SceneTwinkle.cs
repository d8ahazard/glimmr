namespace HueDream.DreamScreen.Scenes {
    public class SceneTwinkle : SceneBase {
        public SceneTwinkle() {
            SetColors(new[] {
                "AAAAAA",
                "000000"
            });
            AnimationTime = .5;
            Mode = AnimationMode.Linear;
            Easing = EasingType.Blend;
        }
    }
}