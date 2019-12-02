using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
            "7d3e1e" // Brownish
            });
            AnimationTime = .75;
            Mode = AnimationMode.Random;
            Easing = EasingType.blend;
        }
    }
}
