using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HueDream.DreamScreen.Scenes {
    public class ScenePop : SceneBase {
        public ScenePop() {
            SetColors(new string[]{
            "fe01ff", // Pinky/purple
            "b847ff", // Purply/purple
            "827dff", // Bluey/blue
            "3db8f6", // LightBlue
            "11e5f6" // Teal

            });
            AnimationTime = .5;
            Mode = AnimationMode.Random;
            Easing = EasingType.fadeIn;
        }
    }
}
