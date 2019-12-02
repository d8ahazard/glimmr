using System;

namespace HueDream.DreamScreen.Scenes {
    [Serializable]
    public abstract class SceneBase {
        public enum AnimationMode { Linear, Reverse, Random, RandomAll };
        public enum EasingType { none, fadeOut, fadeIn, fadeOutIn, blend }
        private string[] colors;

        public string[] GetColors() { return colors; }

        public void SetColors(string[] value) { colors = value; }
        public double AnimationTime { get; set; }
        public AnimationMode Mode { get; set; }
        public EasingType Easing { get; set; }
    }
}
