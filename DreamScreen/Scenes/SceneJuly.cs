using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HueDream.DreamScreen.Scenes {
    public class SceneJuly : SceneBase {
        public SceneJuly() {
            SetColors(new string[]{
            "FF0000", // Red
            "0000FF", // Blue
            "000000" // White

            });
            AnimationTime = .5;
            Mode = AnimationMode.Random;
            Easing = EasingType.none;
        }
    }
}
