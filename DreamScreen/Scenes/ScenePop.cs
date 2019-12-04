namespace HueDream.DreamScreen.Scenes {
    public class ScenePop : SceneBase {
        public ScenePop() {
            SetColors(new[]{
            "fe01ff", // Pinky/purple
            "b847ff", // Purpl/purple
            "827dff", // Blue/blue
            "3db8f6", // LightBlue
            "11e5f6" // Teal

            });
            AnimationTime = 3;
            Mode = AnimationMode.Linear;
            Easing = EasingType.Blend;
        }
    }
}
