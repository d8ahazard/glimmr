namespace HueDream.Models.DreamScreen.Scenes {
    public class SceneFire : SceneBase {
        public SceneFire() {
            SetColors(new[] {
                "d2491a", // Orange red
                "f58600", // Orange
                "f59700", // Tangerine
                "f5dd02", // Yellow
                "f59700", // Tangerine
                "f58600" // Orange
            });
            AnimationTime = .35;
            Mode = AnimationMode.Linear;
            Easing = EasingType.Blend;
        }
    }
}