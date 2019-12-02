using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HueDream.DreamScreen.Scenes {
    public class SceneRainbow : SceneBase {
        public SceneRainbow() {
            SetColors(new string[]{
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
            AnimationTime = 2;
            Mode = AnimationMode.Linear;
            Easing = EasingType.blend;
        }
        
    }
}
