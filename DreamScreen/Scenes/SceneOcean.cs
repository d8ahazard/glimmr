using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HueDream.DreamScreen.Scenes {
    public class SceneOcean : SceneBase {
        public SceneOcean() {
            SetColors(new string[] {
                "02c676", // Sea Green
                "2dbee7", // Sky Blue
                "000000", // White
                "00beff", // Deeper blue
                "00a763" // Deeper green
            });
            AnimationTime = .75;
            Mode = AnimationMode.Random;
            Easing = EasingType.fadeOut;
        }
    }
}
