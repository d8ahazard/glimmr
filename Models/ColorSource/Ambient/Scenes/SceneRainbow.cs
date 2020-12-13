namespace Glimmr.Models.ColorSource.Ambient.Scenes {
    public class SceneRainbow : SceneBase {
        public SceneRainbow() {
            SetColors(new[] {
                "FF0000", // Red
                "ff00ff", // Pink
                "7f00ff", // Violet
                "4b0082", // Indigo
                "0000FF", // Blue
                "00ffff", // Teal
                "00FF00", // Green
                "fff200", // Yellow
                "ff7e00" // Orange
            });
            AnimationTime = 5;
            Mode = AnimationMode.Linear;
            Easing = EasingType.Blend;
        }
    }
}