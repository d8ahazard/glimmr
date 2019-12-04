using System;

namespace HueDream.DreamScreen.Scenes {
    [Serializable]
    public abstract class SceneBase {
        public enum AnimationMode { Linear, Reverse, Random, RandomAll };
        public enum EasingType {
            FadeOut, FadeIn, Blend }
        private string[] colors;

        public string[] GetColors() { return colors; }

        protected void SetColors(string[] value) { colors = value; }
        public double AnimationTime { get; protected set; }
        public AnimationMode Mode { get; protected set; }
        public EasingType Easing { get; protected set; }
    }
}
