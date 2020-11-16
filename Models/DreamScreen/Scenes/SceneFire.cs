namespace Glimmr.Models.DreamScreen.Scenes {
    public class SceneFire : SceneBase {
        public SceneFire() {
            SetColors(new[] {
                "8a3215", // Orange red
                "8a3a00",
                "8a4215", // Orange
                "ab5b00",
                "f57600", // Tangerine
                "a67600", // Yellow
                "f57600", // Tangerine
                "db7600" // Orange
            });
            AnimationTime = .35;
            Mode = AnimationMode.Linear;
            Easing = EasingType.Blend;
        }
    }
}