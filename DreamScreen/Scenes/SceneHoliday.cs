using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HueDream.DreamScreen.Scenes {
    public class SceneHoliday : SceneBase {
        public SceneHoliday() {
            SetColors(new string[]{
            "FF0000", // Red
            "00FF00" // Green
            });
            AnimationTime = .75;
            Mode = AnimationMode.Linear;
            Easing = EasingType.fadeOut;
        }
    }
}
