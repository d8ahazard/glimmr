namespace HueDream.DreamScreen.Scenes {
    public class SceneRandom : SceneBase {
        public SceneRandom() {
            SetColors(new string[]{
            "FF0000", // Red
            "00FF00", // Green
            "0000FF", // Blue
            "00fcff", // Teal
            "a500c3", // Purple

            });
            AnimationTime = 1;
            Mode = AnimationMode.RandomAll;
            Easing = EasingType.blend;
        }
    }
}
