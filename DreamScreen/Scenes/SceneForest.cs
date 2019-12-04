namespace HueDream.DreamScreen.Scenes {
    public class SceneForest : SceneBase {
        public SceneForest() {
            SetColors(new[]{
            "2f6525", // Dark Green
            "0cac00", // Light Green
            "89bc09" // Yellow green
            });
            AnimationTime = 2.5;
            Mode = AnimationMode.Random;
            Easing = EasingType.Blend;
        }
    }
}
