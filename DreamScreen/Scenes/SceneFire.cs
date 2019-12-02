namespace HueDream.DreamScreen.Scenes {
    public class SceneFire : SceneBase {
        public SceneFire() {
            SetColors(new string[]{
            "ff0600", // Red red
            "c8231f", // Deep red
            "d2491a", // Orange red
            "ff8600", // Orange
            "ffba00", // Tangerine
            "fff200", // Yellow
            "ffba00", // Tangerine
            "ff8600", // Orange            
            "7d3e1e" // Brownish
            });
            AnimationTime = .25;
            Mode = AnimationMode.Linear;
            Easing = EasingType.blend;
        }
    }
}
