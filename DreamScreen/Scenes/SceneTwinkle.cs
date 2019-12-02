using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HueDream.DreamScreen.Scenes {
    public class SceneTwinkle : SceneBase {
        public SceneTwinkle() {
            SetColors(new string[] {
                "FFFFFF",
                "000000"
            });
            AnimationTime = .25;
            Mode = AnimationMode.Linear;
            Easing = EasingType.none;
        }
    }
}
